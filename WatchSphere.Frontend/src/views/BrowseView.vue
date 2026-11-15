<template>
  <div class="page">
    <header class="page-head">
      <div>
        <span class="eyebrow">{{ eyebrow }}</span>
        <h1>{{ heading }}</h1>
      </div>

      <p v-if="!loading" class="count num">
        {{ total.toLocaleString('en-US') }} {{ total === 1 ? 'title' : 'titles' }}
      </p>
    </header>

    <div class="filters panel">
      <div class="field">
        <label class="eyebrow" for="filter-search">Title</label>
        <input
          id="filter-search"
          v-model="draft.search"
          type="search"
          placeholder="Search the catalogue"
          @keydown.enter="applyFilters"
        />
      </div>

      <div class="field">
        <label class="eyebrow" for="filter-type">Kind</label>
        <select id="filter-type" v-model="draft.type" @change="applyFilters">
          <option value="">All kinds</option>
          <option value="Anime">Anime</option>
          <option value="Movie">Movies</option>
          <option value="Series">TV shows</option>
        </select>
      </div>

      <div class="field">
        <label class="eyebrow" for="filter-genre">Genre</label>
        <select id="filter-genre" v-model="draft.genreId" @change="applyFilters">
          <option value="">All genres</option>
          <option v-for="genre in genres" :key="genre.id" :value="String(genre.id)">
            {{ genre.name }}
          </option>
        </select>
      </div>

      <div class="field">
        <label class="eyebrow" for="filter-sort">Sort</label>
        <select id="filter-sort" v-model="draft.sortBy" @change="applyFilters">
          <option value="">Newest first</option>
          <option value="popular">Most tracked</option>
          <option value="rating">Highest rated</option>
          <option value="title">Title A–Z</option>
          <option value="year">Release year</option>
        </select>
      </div>

      <div class="field actions">
        <button class="btn" type="button" @click="applyFilters">Apply</button>
        <button v-if="hasFilters" class="btn btn-quiet" type="button" @click="clearFilters">Clear</button>
      </div>
    </div>

    <div v-if="loading" class="poster-grid">
      <div v-for="n in 18" :key="n" class="skeleton grid-skeleton"></div>
    </div>

    <div v-else-if="items.length === 0" class="empty">
      <strong>No titles match these filters.</strong>
      Widen the search or clear the filters to see the whole catalogue.
    </div>

    <div v-else class="poster-grid">
      <MediaCard
        v-for="item in items"
        :key="item.id"
        :media="item"
        :entry="entries[item.id] ?? null"
        @entry-changed="apply"
      />
    </div>

    <nav v-if="totalPages > 1" class="pager" aria-label="Pagination">
      <button class="btn btn-quiet btn-sm" type="button" :disabled="page <= 1" @click="goTo(page - 1)">
        Previous
      </button>

      <span class="pager-state num">Page {{ page }} of {{ totalPages }}</span>

      <button
        class="btn btn-quiet btn-sm"
        type="button"
        :disabled="page >= totalPages"
        @click="goTo(page + 1)"
      >
        Next
      </button>
    </nav>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '../api/client'
import MediaCard from '../components/MediaCard.vue'
import { isLoggedIn } from '../stores/auth'
import { entriesByMediaId, load as loadLibrary, apply } from '../stores/library'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const items = ref([])
const genres = ref([])
const total = ref(0)
const totalPages = ref(1)

const entries = entriesByMediaId

// The query string is the source of truth, so a filtered view can be shared as
// a link and the browser's back button steps through filter changes.
const draft = reactive({
  search: '',
  type: '',
  genreId: '',
  sortBy: ''
})

const page = computed(() => Number(route.query.page ?? 1))

const hasFilters = computed(
  () => Boolean(draft.search || draft.type || draft.genreId || draft.sortBy)
)

const heading = computed(() =>
{
  if (route.query.search)
  {
    return `Results for “${route.query.search}”`
  }

  return { Movie: 'Movies', Series: 'TV Shows', Anime: 'Anime' }[route.query.type] ?? 'Browse'
})

const eyebrow = computed(() =>
{
  if (route.query.search)
  {
    return 'Search'
  }

  return route.query.type ? 'Catalogue' : 'Everything we track'
})

function syncDraftFromRoute()
{
  draft.search = route.query.search ?? ''
  draft.type = route.query.type ?? ''
  draft.genreId = route.query.genreId ?? ''
  draft.sortBy = route.query.sortBy ?? ''
}

function applyFilters()
{
  const query = {}

  if (draft.search.trim()) query.search = draft.search.trim()
  if (draft.type) query.type = draft.type
  if (draft.genreId) query.genreId = draft.genreId
  if (draft.sortBy) query.sortBy = draft.sortBy

  router.push({ path: '/browse', query })
}

function clearFilters()
{
  router.push({ path: '/browse' })
}

function goTo(next)
{
  router.push({ path: '/browse', query: { ...route.query, page: next } })
}

async function fetchPage()
{
  loading.value = true

  try
  {
    const response = await client.get('/Media', {
      params: {
        search: route.query.search || undefined,
        type: route.query.type || undefined,
        genreId: route.query.genreId || undefined,
        sortBy: route.query.sortBy || undefined,
        page: page.value,
        pageSize: 24
      }
    })

    items.value = response.data.data
    total.value = response.data.totalItems
    totalPages.value = response.data.totalPages
  }
  finally
  {
    loading.value = false
  }
}

watch(() => route.fullPath, () =>
{
  syncDraftFromRoute()
  fetchPage()
})

onMounted(async () =>
{
  syncDraftFromRoute()

  const [, genreResponse] = await Promise.all([
    fetchPage(),
    client.get('/Genre').catch(() => ({ data: [] }))
  ])

  genres.value = genreResponse.data

  if (isLoggedIn.value)
  {
    await loadLibrary()
  }
})
</script>

<style scoped>
.count { font-size: 0.8rem; color: var(--ws-dim); }

.filters {
  display: grid;
  grid-template-columns: minmax(200px, 1.6fr) repeat(3, minmax(130px, 1fr)) auto;
  gap: 0.85rem;
  padding: 1rem 1.1rem;
  margin-bottom: 1.8rem;
}

.field { display: flex; flex-direction: column; gap: 0.35rem; min-width: 0; }

.field label { margin-left: 2px; }

.actions { flex-direction: row; align-items: flex-end; gap: 0.5rem; }

.actions .btn { height: 41px; }

.grid-skeleton { aspect-ratio: 2 / 3; border-radius: var(--ws-radius); }

.pager {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 2.6rem;
}

.pager-state { font-size: 0.8rem; color: var(--ws-dim); }

@media (max-width: 1000px) {
  .filters { grid-template-columns: repeat(2, minmax(0, 1fr)); }

  .actions { grid-column: 1 / -1; }
}

@media (max-width: 560px) {
  .filters { grid-template-columns: 1fr; }
}
</style>
