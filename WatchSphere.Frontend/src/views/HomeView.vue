<template>
  <div class="page home">
    <HeroSpotlight
      :items="featured"
      :side="spotlightSide"
      :entries="entries"
      :loading="loading"
      eyebrow="Trending now"
      @entry-changed="onEntryChanged"
    />

    <MediaRail
      v-if="continueWatching.length > 0"
      title="Continue watching"
      eyebrow="Pick up where you left off"
      :items="continueWatching"
      :entries="entries"
      to="/library"
      @entry-changed="onEntryChanged"
    />

    <MediaRail
      title="Popular this week"
      eyebrow="Most tracked"
      :items="popular"
      :entries="entries"
      :loading="loading"
      to="/browse?sortBy=popular"
      @entry-changed="onEntryChanged"
    />

    <MediaRail
      title="Top rated"
      eyebrow="Highest community score"
      :items="topRated"
      :entries="entries"
      :loading="loading"
      to="/browse?sortBy=rating"
      @entry-changed="onEntryChanged"
    />

    <MediaRail
      title="Recently added"
      eyebrow="New in the catalogue"
      :items="recent"
      :entries="entries"
      :loading="loading"
      to="/browse"
      @entry-changed="onEntryChanged"
    />

    <section v-if="!loading && stats" class="ledger">
      <div v-for="stat in ledger" :key="stat.label" class="ledger-cell">
        <span class="ledger-value num">{{ stat.value }}</span>
        <span class="eyebrow">{{ stat.label }}</span>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import client from '../api/client'
import HeroSpotlight from '../components/HeroSpotlight.vue'
import MediaRail from '../components/MediaRail.vue'
import { isLoggedIn } from '../stores/auth'
import { entriesByMediaId, load as loadLibrary, apply } from '../stores/library'

const loading = ref(true)
const popular = ref([])
const topRated = ref([])
const recent = ref([])
const airing = ref([])
const stats = ref(null)

const entries = entriesByMediaId

// Airing titles lead the carousel because they are the ones worth returning
// for; the catalogue's best rated fill in when nothing is currently airing.
const featured = computed(() =>
{
  const source = airing.value.length > 0 ? airing.value : topRated.value

  return source.slice(0, 5)
})

const spotlightSide = computed(() =>
{
  const featuredIds = new Set(featured.value.map(item => item.id))

  // Eight, because the hero is two poster rows tall and anything less leaves
  // the bottom half of that block empty. Recent additions top the list up when
  // the popular rail is short.
  const seen = new Set(featuredIds)
  const picked = []

  for (const item of [...popular.value, ...topRated.value, ...recent.value])
  {
    if (seen.has(item.id))
    {
      continue
    }

    seen.add(item.id)
    picked.push(item)

    if (picked.length === 8)
    {
      break
    }
  }

  return picked
})

const continueWatching = computed(() =>
  Object.values(entries.value)
    .filter(entry => entry.status === 'Watching')
    .sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt))
)

const ledger = computed(() =>
{
  const source = stats.value ?? {}

  return [
    { label: 'Titles catalogued', value: format(source.totalMedia) },
    { label: 'Members', value: format(source.totalUsers) },
    { label: 'Reviews written', value: format(source.totalReviews) },
    { label: 'Episodes tracked', value: format(source.totalEpisodesTracked) }
  ]
})

function format(value)
{
  return (value ?? 0).toLocaleString('en-US')
}

function onEntryChanged(entry)
{
  apply(entry)
}

onMounted(async () =>
{
  try
  {
    const [popularRes, topRes, recentRes, airingRes, statsRes] = await Promise.all([
      client.get('/Media/popular', { params: { take: 16 } }),
      client.get('/Media/top-rated', { params: { take: 16 } }),
      client.get('/Media/recent', { params: { take: 16 } }),
      client.get('/Media/airing', { params: { take: 5 } }),
      client.get('/Media/statistics')
    ])

    popular.value = popularRes.data
    topRated.value = topRes.data
    recent.value = recentRes.data
    airing.value = airingRes.data
    stats.value = statsRes.data
  }
  finally
  {
    loading.value = false
  }

  if (isLoggedIn.value)
  {
    await loadLibrary()
  }
})
</script>

<style scoped>
.home { padding-top: 22px; }

/* A quiet footer of counts rather than a row of stat cards - the numbers are
   context, not the point of the page. */
.ledger {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 1px;
  margin-top: 3.4rem;
  background: var(--ws-line);
  border: 1px solid var(--ws-line);
  border-radius: var(--ws-radius);
  overflow: hidden;
}

.ledger-cell {
  padding: 1.15rem 1.3rem;
  background: var(--ws-panel);
}

.ledger-value {
  display: block;
  margin-bottom: 0.3rem;
  font-size: 1.5rem;
  font-weight: 600;
  letter-spacing: -0.02em;
}
</style>
