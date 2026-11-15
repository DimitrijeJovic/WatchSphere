<template>
  <section v-if="seasons.length" class="guide">
    <div class="section-head">
      <div>
        <span class="eyebrow">{{ totalEpisodes }} episodes across {{ seasons.length }} {{ seasons.length === 1 ? 'season' : 'seasons' }}</span>
        <h2>Episodes</h2>
      </div>

      <button
        v-if="loggedIn && current && current.watchedCount > 0"
        class="link"
        type="button"
        :disabled="busy"
        @click="clearSeason"
      >Clear this season</button>
    </div>

    <nav class="seasons" aria-label="Seasons">
      <button
        v-for="season in seasons"
        :key="season.id"
        type="button"
        class="season"
        :class="{ on: season.id === currentId }"
        @click="currentId = season.id"
      >
        {{ season.seasonNumber === 0 ? 'Specials' : `Season ${season.seasonNumber}` }}

        <span v-if="loggedIn" class="season-count num">
          {{ season.watchedCount }}/{{ season.episodeCount }}
        </span>
        <span v-else class="season-count num">{{ season.episodeCount }}</span>
      </button>
    </nav>

    <div v-if="loggedIn && current" class="season-bar">
      <span class="season-bar-fill" :style="{ width: `${seasonPercent}%` }"></span>
    </div>

    <ol v-if="current" class="episodes">
      <li
        v-for="episode in current.episodes"
        :key="episode.id"
        class="episode"
        :class="{ watched: episode.watched, unaired: isUnaired(episode) }"
      >
        <button
          v-if="loggedIn"
          type="button"
          class="tick"
          :disabled="busy"
          :aria-pressed="episode.watched"
          :aria-label="episode.watched
            ? `Mark episode ${episode.episodeNumber} unwatched`
            : `Mark episode ${episode.episodeNumber} watched`"
          @click="toggle(episode)"
        >
          <svg v-if="episode.watched" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
            <path d="m5 12.5 4.5 4.5L19 7.5"/>
          </svg>
        </button>

        <span v-else class="tick static" aria-hidden="true"></span>

        <span class="ep-number num">{{ episode.episodeNumber }}</span>

        <img
          v-if="episode.stillUrl"
          :src="episode.stillUrl"
          alt=""
          class="still"
          loading="lazy"
        />
        <span v-else class="still still-blank" aria-hidden="true"></span>

        <div class="ep-copy">
          <span class="ep-name">{{ episode.name }}</span>

          <span class="ep-meta num">
            <template v-if="episode.airDate">{{ formatDate(episode.airDate) }}</template>
            <template v-else>Not aired</template>
            <template v-if="episode.runtime"> · {{ episode.runtime }} min</template>
            <template v-if="episode.externalRating"> · {{ episode.externalRating.toFixed(1) }}</template>
          </span>

          <p v-if="episode.overview" class="ep-blurb">{{ episode.overview }}</p>
        </div>

        <div class="ep-actions">
          <select
            v-if="loggedIn && episode.watched"
            class="ep-score"
            :value="episode.myRating ?? ''"
            :disabled="busy"
            aria-label="Your score for this episode"
            @change="score(episode, $event.target.value)"
          >
            <option value="">–</option>
            <option v-for="n in 10" :key="n" :value="11 - n">{{ 11 - n }}</option>
          </select>

          <button
            v-if="loggedIn && !episode.watched"
            class="through"
            type="button"
            :disabled="busy"
            title="Mark this and everything before it as watched"
            @click="watchedThrough(episode)"
          >Up to here</button>
        </div>
      </li>
    </ol>
  </section>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import client from '../api/client'
import { isLoggedIn } from '../stores/auth'

const props = defineProps({
  mediaId: { type: Number, required: true }
})

const emit = defineEmits(['progress'])

const loggedIn = isLoggedIn

const seasons = ref([])
const currentId = ref(null)
const busy = ref(false)

const current = computed(
  () => seasons.value.find(season => season.id === currentId.value) ?? null
)

const totalEpisodes = computed(
  () => seasons.value.reduce((sum, season) => sum + season.episodeCount, 0)
)

const seasonPercent = computed(() =>
{
  if (!current.value || current.value.episodeCount === 0)
  {
    return 0
  }

  return (current.value.watchedCount / current.value.episodeCount) * 100
})

function formatDate(value)
{
  return new Date(value).toLocaleDateString('en-GB', {
    day: 'numeric', month: 'short', year: 'numeric'
  })
}

function isUnaired(episode)
{
  return !episode.airDate || new Date(episode.airDate) > new Date()
}

async function load()
{
  const response = await client.get(`/Episode/media/${props.mediaId}`)

  seasons.value = response.data

  if (seasons.value.length === 0)
  {
    return
  }

  // Open on the season still in progress, since that is the one being
  // watched; failing that, the first with anything left.
  const inProgress = seasons.value.find(
    season => season.watchedCount > 0 && season.watchedCount < season.episodeCount)

  const unstarted = seasons.value.find(season => season.watchedCount === 0)

  currentId.value = (inProgress ?? unstarted ?? seasons.value[0]).id
}

/** Applies the counters the API sends back after a change. */
function applyProgress(result)
{
  const season = seasons.value.find(s => s.id === result.seasonId)

  if (season)
  {
    season.watchedCount = result.seasonWatchedCount
  }

  const episode = season?.episodes.find(e => e.id === result.episodeId)

  if (episode)
  {
    episode.watched = result.watched
    episode.myRating = result.myRating
  }

  emit('progress', result)
}

async function run(request)
{
  busy.value = true

  try
  {
    const response = await request()

    applyProgress(response.data)
  }
  finally
  {
    busy.value = false
  }
}

function toggle(episode)
{
  return run(() => client.put(`/Episode/${episode.id}/watched`, {
    watched: !episode.watched
  }))
}

function score(episode, value)
{
  return run(() => client.put(`/Episode/${episode.id}/watched`, {
    watched: true,
    rating: value === '' ? null : Number(value),
    clearRating: value === ''
  }))
}

function watchedThrough(episode)
{
  return run(async () =>
  {
    const response = await client.post(`/Episode/${episode.id}/watched-through`)

    // Several seasons can change at once here, so the list is re-read rather
    // than patched row by row.
    await load()

    return response
  })
}

async function clearSeason()
{
  busy.value = true

  try
  {
    const response = await client.delete(`/Episode/season/${currentId.value}/watched`)

    await load()

    emit('progress', response.data)
  }
  finally
  {
    busy.value = false
  }
}

watch(() => props.mediaId, load)
watch(isLoggedIn, load)

onMounted(load)

defineExpose({ reload: load })
</script>

<style scoped>
.guide { display: grid; gap: 0.9rem; }

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--ws-faint);
  font-size: 0.76rem;
  font-weight: 600;
  transition: color 0.15s var(--ws-ease);
}

.link:hover:not(:disabled) { color: #ff6070; }

/* --- season selector ------------------------------------------------------ */

.seasons { display: flex; gap: 0.4rem; overflow-x: auto; padding-bottom: 3px; scrollbar-width: none; }

.seasons::-webkit-scrollbar { display: none; }

.season {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.42rem 0.8rem;
  border: 1px solid var(--ws-line);
  border-radius: 999px;
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.8rem;
  font-weight: 600;
  white-space: nowrap;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.season:hover { color: var(--ws-text); }

.season.on { background: var(--ws-red); border-color: var(--ws-red); color: #fff; }

.season-count { font-size: 0.72rem; opacity: 0.72; }

.season-bar { height: 3px; border-radius: 999px; background: rgba(255, 255, 255, 0.08); overflow: hidden; }

.season-bar-fill { display: block; height: 100%; background: var(--ws-red); transition: width 0.3s var(--ws-ease); }

/* --- episode rows --------------------------------------------------------- */

.episodes { list-style: none; margin: 0; padding: 0; display: grid; gap: 1px; }

.episode {
  display: grid;
  grid-template-columns: 26px 26px 108px minmax(0, 1fr) auto;
  align-items: center;
  gap: 0.7rem;
  padding: 0.6rem 0.5rem;
  border-radius: var(--ws-radius-sm);
  transition: background 0.15s var(--ws-ease);
}

.episode:hover { background: rgba(255, 255, 255, 0.03); }

.episode.unaired { opacity: 0.55; }

/* --- the tick ------------------------------------------------------------- */

.tick {
  display: grid;
  place-items: center;
  width: 22px;
  height: 22px;
  padding: 0;
  border: 1.5px solid var(--ws-line-strong);
  border-radius: 6px;
  background: transparent;
  color: #fff;
  transition: background 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.tick svg { width: 13px; height: 13px; }

.tick:hover:not(:disabled) { border-color: var(--ws-dim); }

.episode.watched .tick { background: var(--ws-red); border-color: var(--ws-red); }

.tick.static { border-style: dashed; opacity: 0.4; }

.ep-number { font-size: 0.82rem; color: var(--ws-faint); text-align: right; }

.episode.watched .ep-number { color: var(--ws-dim); }

.still {
  width: 108px;
  aspect-ratio: 16 / 9;
  object-fit: cover;
  border-radius: 5px;
  background: var(--ws-card);
}

.still-blank { display: block; }

.ep-copy { min-width: 0; }

.ep-name {
  display: block;
  font-size: 0.88rem;
  font-weight: 600;
  color: var(--ws-dim);
  transition: color 0.15s var(--ws-ease);
}

.episode.watched .ep-name { color: var(--ws-text); }

.ep-meta { display: block; margin-top: 0.15rem; font-size: 0.72rem; color: var(--ws-faint); }

.ep-blurb {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-top: 0.3rem;
  font-size: 0.79rem;
  line-height: 1.5;
  color: var(--ws-faint);
}

/* --- row actions ---------------------------------------------------------- */

.ep-actions { display: flex; align-items: center; justify-content: flex-end; min-width: 84px; }

.ep-score { width: auto; padding: 0.28rem 0.4rem; font-size: 0.78rem; }

/* Kept invisible until the row is hovered, so a long list is not a wall of
   buttons; focus reveals it too, for keyboard use. */
.through {
  padding: 0.24rem 0.55rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 6px;
  background: transparent;
  color: var(--ws-faint);
  font-size: 0.72rem;
  font-weight: 600;
  white-space: nowrap;
  opacity: 0;
  transition: opacity 0.15s var(--ws-ease), color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.episode:hover .through, .through:focus-visible { opacity: 1; }

.through:hover:not(:disabled) { color: var(--ws-text); border-color: var(--ws-dim); }

@media (max-width: 780px) {
  .episode { grid-template-columns: 26px 24px minmax(0, 1fr) auto; gap: 0.55rem; }

  .still { display: none; }

  .ep-blurb { display: none; }

  .through { opacity: 1; }
}
</style>
