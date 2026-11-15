<template>
  <div class="tracker panel">
    <div class="tracker-head">
      <span class="eyebrow">Your tracking</span>

      <button v-if="entry" class="remove" type="button" :disabled="busy" @click="remove">
        Remove
      </button>
    </div>

    <p v-if="error" class="notice notice-error">{{ error }}</p>

    <template v-if="!entry">
      <p class="pitch">Add this title to start counting episodes and give it a score.</p>

      <button class="btn add" type="button" :disabled="busy" @click="add">
        Add to library
      </button>
    </template>

    <template v-else>
      <!-- Progress first: it is the number that changes most often. -->
      <div class="progress-block">
        <div class="progress-line">
          <span class="count num">
            {{ entry.episodesWatched }}<span class="slash">/</span>{{ totalLabel }}
          </span>

          <span class="count-label">episodes</span>

          <div class="stepper">
            <button
              type="button"
              class="step"
              :disabled="busy || entry.episodesWatched === 0"
              aria-label="Remove one episode"
              @click="setEpisodes(entry.episodesWatched - 1)"
            >−</button>

            <button
              type="button"
              class="step primary"
              :disabled="busy || !canAdvance"
              aria-label="Add one episode"
              @click="advance"
            >+1</button>
          </div>
        </div>

        <div class="bar" role="progressbar" :aria-valuenow="entry.progressPercent" aria-valuemin="0" aria-valuemax="100">
          <span class="bar-fill" :style="{ width: `${entry.progressPercent}%` }"></span>
        </div>

        <div v-if="total" class="ticks">
          <button
            v-for="tick in ticks"
            :key="tick"
            type="button"
            class="tick"
            :class="{ reached: tick <= entry.episodesWatched }"
            :disabled="busy"
            :aria-label="`Set progress to episode ${tick}`"
            @click="setEpisodes(tick)"
          ></button>
        </div>
      </div>

      <div class="grid">
        <label class="field">
          <span class="eyebrow">Status</span>
          <select :value="entry.status" :disabled="busy" @change="patch({ status: $event.target.value })">
            <option value="Watching">Watching</option>
            <option value="Completed">Completed</option>
            <option value="Planned">Planned</option>
            <option value="OnHold">On hold</option>
            <option value="Dropped">Dropped</option>
          </select>
        </label>

        <label class="field">
          <span class="eyebrow">Your score</span>
          <select :value="entry.score ?? ''" :disabled="busy" @change="onScore($event.target.value)">
            <option value="">Not scored</option>
            <option v-for="n in 10" :key="n" :value="11 - n">{{ 11 - n }}</option>
          </select>
        </label>

        <label class="field">
          <span class="eyebrow">Rewatches</span>
          <input
            type="number"
            min="0"
            max="999"
            :value="entry.rewatchCount"
            :disabled="busy"
            @change="patch({ rewatchCount: Number($event.target.value) })"
          />
        </label>

        <label class="field favourite">
          <span class="eyebrow">Favourite</span>
          <button
            type="button"
            class="fav-btn"
            :class="{ on: entry.isFavorite }"
            :disabled="busy"
            @click="patch({ isFavorite: !entry.isFavorite })"
          >
            {{ entry.isFavorite ? '★ Favourited' : '☆ Mark favourite' }}
          </button>
        </label>
      </div>

      <label class="field">
        <span class="eyebrow">Private notes</span>
        <textarea
          v-model="notes"
          rows="3"
          placeholder="Only you can read these."
          :disabled="busy"
          @blur="saveNotes"
        ></textarea>
      </label>

      <p v-if="entry.startedAt" class="dates num">
        Started {{ formatDate(entry.startedAt) }}<template v-if="entry.finishedAt"> · Finished {{ formatDate(entry.finishedAt) }}</template>
      </p>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import client, { errorMessage } from '../api/client'

const props = defineProps({
  mediaId: { type: Number, required: true },
  entry: { type: Object, default: null },
  /** Used to size the progress bar before the entry exists. */
  totalEpisodes: { type: Number, default: null },
  type: { type: String, default: '' }
})

const emit = defineEmits(['changed', 'removed'])

const busy = ref(false)
const error = ref('')
const notes = ref(props.entry?.notes ?? '')

const total = computed(() =>
{
  if (props.type === 'Movie')
  {
    return 1
  }

  return props.entry?.totalEpisodes ?? props.totalEpisodes ?? null
})

const totalLabel = computed(() => total.value ?? '?')

const canAdvance = computed(
  () => total.value === null || props.entry.episodesWatched < total.value
)

// One tick per episode reads well up to about two cours; past that the bar
// alone carries the story and the ticks would turn into noise.
const ticks = computed(() =>
{
  if (!total.value || total.value > 26)
  {
    return []
  }

  return Array.from({ length: total.value }, (_, i) => i + 1)
})

watch(() => props.entry?.notes, value => { notes.value = value ?? '' })

function formatDate(value)
{
  return new Date(value).toLocaleDateString('en-GB', {
    day: 'numeric', month: 'short', year: 'numeric'
  })
}

async function run(request)
{
  busy.value = true
  error.value = ''

  try
  {
    const response = await request()

    if (response?.data)
    {
      emit('changed', response.data)
    }

    return true
  }
  catch (e)
  {
    error.value = errorMessage(e, 'Could not save that change.')

    return false
  }
  finally
  {
    busy.value = false
  }
}

function add()
{
  return run(() => client.post('/UserLibrary', {
    mediaId: props.mediaId,
    status: 'Watching',
    isFavorite: false
  }))
}

function patch(body)
{
  return run(() => client.put(`/UserLibrary/${props.entry.id}`, body))
}

function advance()
{
  return run(() => client.post(`/UserLibrary/${props.entry.id}/episode`))
}

function setEpisodes(value)
{
  return patch({ episodesWatched: Math.max(0, value) })
}

function onScore(value)
{
  // An empty selection has to be sent as an explicit clear, since a missing
  // field means "leave this alone".
  return value === ''
    ? patch({ clearScore: true })
    : patch({ score: Number(value) })
}

function saveNotes()
{
  if (notes.value === (props.entry?.notes ?? ''))
  {
    return
  }

  return patch({ notes: notes.value })
}

async function remove()
{
  busy.value = true
  error.value = ''

  try
  {
    await client.delete(`/UserLibrary/${props.entry.id}`)

    emit('removed', props.mediaId)
  }
  catch (e)
  {
    error.value = errorMessage(e, 'Could not remove this title.')
  }
  finally
  {
    busy.value = false
  }
}
</script>

<style scoped>
.tracker { padding: 1.15rem 1.2rem; display: grid; gap: 1rem; }

.tracker-head { display: flex; align-items: center; justify-content: space-between; }

.remove {
  padding: 0;
  border: 0;
  background: none;
  color: var(--ws-faint);
  font-size: 0.74rem;
  font-weight: 600;
  transition: color 0.15s var(--ws-ease);
}

.remove:hover:not(:disabled) { color: #ff6070; }

.pitch { font-size: 0.86rem; color: var(--ws-dim); }

.add { width: 100%; }

/* --- progress ------------------------------------------------------------- */

.progress-block { display: grid; gap: 0.55rem; }

.progress-line { display: flex; align-items: baseline; gap: 0.5rem; }

.count { font-size: 1.5rem; font-weight: 600; letter-spacing: -0.02em; }

.slash { margin: 0 2px; color: var(--ws-faint); font-weight: 400; }

.count-label { font-size: 0.76rem; color: var(--ws-faint); }

.stepper { margin-left: auto; display: flex; gap: 5px; }

.step {
  min-width: 34px;
  height: 30px;
  padding: 0 0.5rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 7px;
  background: transparent;
  color: var(--ws-dim);
  font-family: 'IBM Plex Mono', monospace;
  font-size: 0.82rem;
  font-weight: 600;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.step:hover:not(:disabled) { background: rgba(255, 255, 255, 0.08); color: var(--ws-text); }

.step.primary { background: var(--ws-red); border-color: var(--ws-red); color: #fff; }

.step.primary:hover:not(:disabled) { background: var(--ws-red-hot); border-color: var(--ws-red-hot); color: #fff; }

.step:disabled { opacity: 0.35; cursor: not-allowed; }

.bar { height: 5px; border-radius: 999px; background: rgba(255, 255, 255, 0.1); overflow: hidden; }

.bar-fill { display: block; height: 100%; background: var(--ws-red); transition: width 0.3s var(--ws-ease); }

/* One segment per episode, clickable, so jumping to episode nine is a single
   press rather than nine presses of the plus button. */
.ticks { display: flex; gap: 3px; }

.tick {
  flex: 1;
  height: 14px;
  padding: 0;
  border: 0;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.07);
  transition: background 0.15s var(--ws-ease);
}

.tick:hover:not(:disabled) { background: rgba(255, 255, 255, 0.22); }

.tick.reached { background: rgba(225, 29, 46, 0.55); }

.tick.reached:hover:not(:disabled) { background: var(--ws-red); }

/* --- fields --------------------------------------------------------------- */

.grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.7rem; }

.field { display: flex; flex-direction: column; gap: 0.32rem; min-width: 0; }

.fav-btn {
  padding: 0.7rem 0.85rem;
  border: 1px solid var(--ws-line);
  border-radius: var(--ws-radius-sm);
  background: var(--ws-raise);
  color: var(--ws-dim);
  font-size: 0.84rem;
  font-weight: 500;
  text-align: left;
  transition: color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.fav-btn:hover:not(:disabled) { color: var(--ws-text); }

.fav-btn.on { color: var(--ws-amber); border-color: rgba(242, 177, 52, 0.35); }

.dates { font-size: 0.73rem; color: var(--ws-faint); }
</style>
