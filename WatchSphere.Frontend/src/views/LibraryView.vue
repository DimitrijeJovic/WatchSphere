<template>
  <div class="page">
    <header class="page-head">
      <div>
        <span class="eyebrow">Your collection</span>
        <h1>My Library</h1>
      </div>

      <div class="view-toggle" role="group" aria-label="Layout">
        <button type="button" :class="{ on: layout === 'grid' }" @click="setLayout('grid')">Grid</button>
        <button type="button" :class="{ on: layout === 'list' }" @click="setLayout('list')">List</button>
      </div>
    </header>

    <nav class="tabs" aria-label="Filter by status">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        type="button"
        class="tab"
        :class="{ on: filter === tab.key }"
        @click="filter = tab.key"
      >
        {{ tab.label }}
        <span class="tab-count num">{{ counts[tab.key] ?? 0 }}</span>
      </button>
    </nav>

    <p v-if="loading" class="muted">Loading your library…</p>

    <div v-else-if="visible.length === 0" class="empty">
      <strong>{{ emptyHeading }}</strong>
      <router-link to="/browse" class="back-link">Browse the catalogue</router-link> to add something.
    </div>

    <div v-else-if="layout === 'grid'" class="poster-grid">
      <MediaCard
        v-for="entry in visible"
        :key="entry.id"
        :media="entry"
        :entry="entry"
        @entry-changed="onChanged"
      />
    </div>

    <!-- The dense view is the one to use when actually updating progress on a
         season's worth of titles in a sitting. -->
    <table v-else class="list">
      <thead>
        <tr>
          <th class="col-title">Title</th>
          <th class="col-status">Status</th>
          <th class="col-progress">Progress</th>
          <th class="col-score">Score</th>
          <th class="col-actions"><span class="sr-only">Actions</span></th>
        </tr>
      </thead>

      <tbody>
        <tr v-for="entry in visible" :key="entry.id">
          <td class="col-title">
            <router-link :to="`/media/${entry.mediaId}`" class="row-title">
              <img v-if="entry.posterUrl" :src="entry.posterUrl" alt="" class="thumb" loading="lazy" />
              <span>
                <span class="row-name">{{ entry.title }}</span>
                <span class="row-meta num">{{ entry.type }} · {{ entry.releaseYear }}</span>
              </span>
            </router-link>
          </td>

          <td class="col-status">
            <select
              :value="entry.status"
              :disabled="busyId === entry.id"
              aria-label="Status"
              @change="patch(entry, { status: $event.target.value })"
            >
              <option value="Watching">Watching</option>
              <option value="Completed">Completed</option>
              <option value="Planned">Planned</option>
              <option value="OnHold">On hold</option>
              <option value="Dropped">Dropped</option>
            </select>
          </td>

          <td class="col-progress">
            <div class="row-progress">
              <span class="row-count num">
                {{ entry.episodesWatched }}<span class="slash">/</span>{{ entry.totalEpisodes ?? '?' }}
              </span>

              <span class="row-bar">
                <span class="row-bar-fill" :style="{ width: `${entry.progressPercent}%` }"></span>
              </span>

              <button
                type="button"
                class="row-bump"
                :disabled="busyId === entry.id || !canAdvance(entry)"
                :aria-label="`Add one episode to ${entry.title}`"
                @click="advance(entry)"
              >+1</button>
            </div>
          </td>

          <td class="col-score">
            <select
              :value="entry.score ?? ''"
              :disabled="busyId === entry.id"
              aria-label="Score"
              @change="onScore(entry, $event.target.value)"
            >
              <option value="">–</option>
              <option v-for="n in 10" :key="n" :value="11 - n">{{ 11 - n }}</option>
            </select>
          </td>

          <td class="col-actions">
            <button
              type="button"
              class="fav"
              :class="{ on: entry.isFavorite }"
              :disabled="busyId === entry.id"
              :aria-label="entry.isFavorite ? 'Remove from favourites' : 'Mark as favourite'"
              @click="patch(entry, { isFavorite: !entry.isFavorite })"
            >{{ entry.isFavorite ? '★' : '☆' }}</button>

            <button
              type="button"
              class="drop"
              :disabled="busyId === entry.id"
              :aria-label="`Remove ${entry.title} from your library`"
              @click="remove(entry)"
            >×</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import client from '../api/client'
import MediaCard from '../components/MediaCard.vue'
import { entriesByMediaId, load, apply, forget } from '../stores/library'

const LAYOUT_KEY = 'watchsphere.library.layout'

const tabs = [
  { key: 'All', label: 'All' },
  { key: 'Watching', label: 'Watching' },
  { key: 'Completed', label: 'Completed' },
  { key: 'Planned', label: 'Planned' },
  { key: 'OnHold', label: 'On hold' },
  { key: 'Dropped', label: 'Dropped' },
  { key: 'Favorites', label: 'Favourites' }
]

const loading = ref(true)
const filter = ref('All')
const busyId = ref(null)
const layout = ref(localStorage.getItem(LAYOUT_KEY) === 'list' ? 'list' : 'grid')

const entries = computed(() =>
  Object.values(entriesByMediaId.value)
    .sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt))
)

const counts = computed(() =>
{
  const tally = { All: entries.value.length, Favorites: 0 }

  for (const entry of entries.value)
  {
    tally[entry.status] = (tally[entry.status] ?? 0) + 1

    if (entry.isFavorite)
    {
      tally.Favorites += 1
    }
  }

  return tally
})

const visible = computed(() =>
{
  if (filter.value === 'All')
  {
    return entries.value
  }

  if (filter.value === 'Favorites')
  {
    return entries.value.filter(entry => entry.isFavorite)
  }

  return entries.value.filter(entry => entry.status === filter.value)
})

const emptyHeading = computed(() =>
  filter.value === 'All'
    ? 'Your library is empty.'
    : `Nothing filed under ${tabs.find(tab => tab.key === filter.value).label.toLowerCase()}.`
)

function setLayout(next)
{
  layout.value = next
  localStorage.setItem(LAYOUT_KEY, next)
}

function canAdvance(entry)
{
  return entry.totalEpisodes === null
    || entry.totalEpisodes === undefined
    || entry.episodesWatched < entry.totalEpisodes
}

function onChanged(entry)
{
  apply(entry)
}

async function patch(entry, body)
{
  busyId.value = entry.id

  try
  {
    const response = await client.put(`/UserLibrary/${entry.id}`, body)

    apply(response.data)
  }
  finally
  {
    busyId.value = null
  }
}

async function advance(entry)
{
  busyId.value = entry.id

  try
  {
    const response = await client.post(`/UserLibrary/${entry.id}/episode`)

    apply(response.data)
  }
  finally
  {
    busyId.value = null
  }
}

function onScore(entry, value)
{
  return value === ''
    ? patch(entry, { clearScore: true })
    : patch(entry, { score: Number(value) })
}

async function remove(entry)
{
  if (!window.confirm(`Remove ${entry.title} from your library?`))
  {
    return
  }

  busyId.value = entry.id

  try
  {
    await client.delete(`/UserLibrary/${entry.id}`)

    forget(entry.mediaId)
  }
  finally
  {
    busyId.value = null
  }
}

onMounted(async () =>
{
  await load(true)

  loading.value = false
})
</script>

<style scoped>
.muted { color: var(--ws-dim); font-size: 0.88rem; }

.back-link { color: var(--ws-red-hot); font-weight: 600; }

.view-toggle { display: flex; gap: 2px; padding: 3px; border: 1px solid var(--ws-line); border-radius: 999px; }

.view-toggle button {
  padding: 0.32rem 0.85rem;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: var(--ws-faint);
  font-size: 0.78rem;
  font-weight: 600;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease);
}

.view-toggle button.on { background: rgba(255, 255, 255, 0.1); color: var(--ws-text); }

/* --- tabs ----------------------------------------------------------------- */

.tabs {
  display: flex;
  gap: 0.4rem;
  overflow-x: auto;
  padding-bottom: 4px;
  margin-bottom: 1.6rem;
  scrollbar-width: none;
}

.tabs::-webkit-scrollbar { display: none; }

.tab {
  display: inline-flex;
  align-items: center;
  gap: 0.42rem;
  padding: 0.45rem 0.85rem;
  border: 1px solid var(--ws-line);
  border-radius: 999px;
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.82rem;
  font-weight: 600;
  white-space: nowrap;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.tab:hover { color: var(--ws-text); }

.tab.on { background: var(--ws-red); border-color: var(--ws-red); color: #fff; }

.tab-count { font-size: 0.72rem; opacity: 0.72; }

/* --- list ----------------------------------------------------------------- */

.list { width: 100%; border-collapse: collapse; font-size: 0.86rem; }

.list th {
  padding: 0 0.7rem 0.6rem;
  border-bottom: 1px solid var(--ws-line);
  font-size: 0.68rem;
  font-weight: 600;
  font-stretch: 118%;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--ws-faint);
  text-align: left;
}

.list td { padding: 0.55rem 0.7rem; border-bottom: 1px solid var(--ws-line); vertical-align: middle; }

.list tbody tr:hover { background: rgba(255, 255, 255, 0.025); }

.row-title { display: flex; align-items: center; gap: 0.7rem; min-width: 0; }

.thumb { width: 34px; height: 50px; object-fit: cover; border-radius: 5px; flex-shrink: 0; background: var(--ws-card); }

.row-name { display: block; font-weight: 600; }

.row-meta { display: block; font-size: 0.72rem; color: var(--ws-faint); }

.col-status select, .col-score select { padding: 0.36rem 0.5rem; font-size: 0.8rem; }

.col-score { width: 78px; }

.col-status { width: 140px; }

.col-progress { width: 220px; }

.col-actions { width: 74px; text-align: right; }

.row-progress { display: flex; align-items: center; gap: 0.5rem; }

.row-count { font-size: 0.78rem; color: var(--ws-dim); white-space: nowrap; }

.slash { margin: 0 1px; color: var(--ws-faint); }

.row-bar { flex: 1; height: 4px; border-radius: 999px; background: rgba(255, 255, 255, 0.1); overflow: hidden; }

.row-bar-fill { display: block; height: 100%; background: var(--ws-red); transition: width 0.3s var(--ws-ease); }

.row-bump {
  padding: 0.16rem 0.42rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 5px;
  background: transparent;
  color: var(--ws-dim);
  font-family: 'IBM Plex Mono', monospace;
  font-size: 0.72rem;
  font-weight: 600;
}

.row-bump:hover:not(:disabled) { background: var(--ws-red); border-color: var(--ws-red); color: #fff; }

.row-bump:disabled { opacity: 0.35; cursor: not-allowed; }

.fav, .drop {
  padding: 0.1rem 0.3rem;
  border: 0;
  background: none;
  color: var(--ws-faint);
  font-size: 1rem;
  transition: color 0.15s var(--ws-ease);
}

.fav:hover, .fav.on { color: var(--ws-amber); }

.drop:hover { color: #ff6070; }

.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }

@media (max-width: 820px) {
  .col-status, .col-score { display: none; }

  .col-progress { width: auto; }
}
</style>
