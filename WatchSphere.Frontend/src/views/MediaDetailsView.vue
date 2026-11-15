<template>
  <div v-if="loading" class="page">
    <div class="skeleton hero-skeleton"></div>
  </div>

  <div v-else-if="!media" class="page">
    <div class="empty">
      <strong>That title is not in the catalogue.</strong>
      It may have been removed. <router-link to="/browse" class="back-link">Browse everything</router-link>.
    </div>
  </div>

  <div v-else class="details">
    <header class="banner">
      <img v-if="bannerImage" :src="bannerImage" alt="" class="banner-image" />
      <div class="banner-wash"></div>

      <div class="banner-inner">
        <img
          v-if="media.posterUrl"
          :src="media.posterUrl"
          :alt="media.title"
          class="poster"
        />

        <div class="headline">
          <div class="kinds">
            <span class="chip">{{ media.type }}</span>
            <span v-if="media.airingStatus === 'Airing'" class="chip live">Airing now</span>
            <span v-else-if="media.airingStatus === 'Upcoming'" class="chip">Upcoming</span>
            <span v-if="media.studio" class="chip">{{ media.studio }}</span>
          </div>

          <h1>{{ media.title }}</h1>

          <div class="facts num">
            <span>{{ media.releaseYear }}</span>
            <span v-if="episodeFact" class="sep">·</span>
            <span v-if="episodeFact">{{ episodeFact }}</span>
            <span v-if="media.episodeDuration" class="sep">·</span>
            <span v-if="media.episodeDuration">{{ media.episodeDuration }} min</span>
          </div>

          <div v-if="media.genres.length" class="genres">
            <router-link
              v-for="genre in media.genres"
              :key="genre.id"
              :to="`/browse?genreId=${genre.id}`"
              class="genre"
            >{{ genre.name }}</router-link>
          </div>
        </div>
      </div>
    </header>

    <div class="page body-grid">
      <div class="main">
        <p v-if="media.description" class="synopsis">{{ media.description }}</p>

        <CreditsSection :media-id="media.id" />

        <EpisodeGuide
          ref="guide"
          :media-id="media.id"
          @progress="onEpisodeProgress"
        />

        <RelatedTitles :media-id="media.id" />

        <ReviewList :media-id="media.id" @changed="reload" />
      </div>

      <aside class="rail">
        <section class="scorecard panel">
          <div class="score-lead">
            <span class="score-value num">
              {{ media.averageRating > 0 ? media.averageRating.toFixed(1) : '—' }}
            </span>

            <div class="score-side">
              <StarRating :value="media.averageRating" :size="15" />
              <span class="score-note num">
                {{ media.reviewsCount }} {{ media.reviewsCount === 1 ? 'review' : 'reviews' }}
              </span>
            </div>
          </div>

          <!-- The histogram says something the average cannot: whether people
               agree about a title or are split over it. -->
          <div v-if="media.reviewsCount > 0" class="histogram">
            <div v-for="row in histogram" :key="row.rating" class="hist-row">
              <span class="hist-label num">{{ row.rating }}</span>
              <span class="hist-track"><span class="hist-fill" :style="{ width: `${row.percent}%` }"></span></span>
              <span class="hist-count num">{{ row.count }}</span>
            </div>
          </div>

          <!-- What the wider world thinks, next to what this site does. The
               three rarely agree, and the gap is often the interesting part. -->
          <div v-if="externalScores.length" class="elsewhere">
            <span class="eyebrow">Elsewhere</span>

            <div v-for="score in externalScores" :key="score.label" class="elsewhere-row">
              <span class="elsewhere-name">{{ score.label }}</span>
              <span class="elsewhere-value num">{{ score.value }}</span>
              <span v-if="score.votes" class="elsewhere-votes num">{{ score.votes }}</span>
            </div>
          </div>

          <div class="counts">
            <div>
              <span class="count-value num">{{ media.membersCount }}</span>
              <span class="eyebrow">Tracking</span>
            </div>
            <div>
              <span class="count-value num">{{ media.favoritesCount }}</span>
              <span class="eyebrow">Favourited</span>
            </div>
          </div>
        </section>

        <LibraryTracker
          v-if="loggedIn"
          :media-id="media.id"
          :entry="entry"
          :total-episodes="media.totalEpisodes"
          :type="media.type"
          @changed="onEntryChanged"
          @removed="onEntryRemoved"
        />

        <div v-else class="panel signin-prompt">
          <p>Sign in to track episodes, score this title and write a review.</p>
          <router-link :to="{ path: '/login', query: { redirect: route.fullPath } }" class="btn">
            Sign in
          </router-link>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import client from '../api/client'
import CreditsSection from '../components/CreditsSection.vue'
import EpisodeGuide from '../components/EpisodeGuide.vue'
import LibraryTracker from '../components/LibraryTracker.vue'
import RelatedTitles from '../components/RelatedTitles.vue'
import ReviewList from '../components/ReviewList.vue'
import StarRating from '../components/StarRating.vue'
import { isLoggedIn } from '../stores/auth'
import { apply, forget } from '../stores/library'

const route = useRoute()

const loggedIn = isLoggedIn

const loading = ref(true)
const media = ref(null)
const entry = ref(null)
const guide = ref(null)

const mediaId = computed(() => Number(route.params.id))

const bannerImage = computed(() => media.value?.backdropUrl || media.value?.posterUrl || '')

const episodeFact = computed(() =>
{
  if (!media.value || media.value.type === 'Movie' || !media.value.totalEpisodes)
  {
    return ''
  }

  return `${media.value.totalEpisodes} episodes`
})

const externalScores = computed(() =>
{
  const m = media.value

  if (!m)
  {
    return []
  }

  const rows = []

  if (m.imdbRating)
  {
    rows.push({
      label: 'IMDb',
      value: m.imdbRating.toFixed(1),
      votes: compact(m.imdbVotes)
    })
  }

  // The source's own score, named after whichever catalogue it came from.
  if (m.externalRating)
  {
    rows.push({
      label: { tmdb: 'TMDB', kitsu: 'Kitsu', tvmaze: 'TVmaze' }[m.source] ?? 'Source',
      value: m.externalRating.toFixed(1),
      votes: compact(m.externalVotes)
    })
  }

  return rows
})

/** 2673688 reads as 2.7M; the exact figure is noise at this size. */
function compact(value)
{
  if (!value)
  {
    return ''
  }

  if (value >= 1000000)
  {
    return `${(value / 1000000).toFixed(1)}M`
  }

  if (value >= 1000)
  {
    return `${Math.round(value / 1000)}K`
  }

  return String(value)
}

const histogram = computed(() =>
{
  const buckets = media.value?.ratingDistribution ?? []
  const peak = Math.max(1, ...buckets.map(bucket => bucket.count))

  return Array.from({ length: 10 }, (_, i) =>
  {
    const rating = 10 - i
    const count = buckets.find(bucket => bucket.rating === rating)?.count ?? 0

    return { rating, count, percent: (count / peak) * 100 }
  })
})

async function loadMedia()
{
  const response = await client.get(`/Media/${mediaId.value}`)

  media.value = response.data
}

async function loadEntry()
{
  if (!loggedIn.value)
  {
    return
  }

  // 204 means the title is not in the caller's library, which is a normal
  // answer rather than an error.
  const response = await client.get(`/UserLibrary/me/media/${mediaId.value}`)

  entry.value = response.status === 204 ? null : response.data
}

/** Re-reads the header after a review changes the average. */
async function reload()
{
  await loadMedia()
}

function onEntryChanged(updated)
{
  entry.value = updated
  apply(updated)

  // Ticking episodes and moving the counter are two views of the same thing,
  // so a change to one has to be reflected in the other.
  guide.value?.reload()
}

/** Folds a per-episode change back into the title-level tracker. */
function onEpisodeProgress(result)
{
  if (entry.value && result.episodesWatched !== null && result.episodesWatched !== undefined)
  {
    entry.value = {
      ...entry.value,
      episodesWatched: result.episodesWatched,
      progressPercent: result.progressPercent,
      status: result.status ?? entry.value.status
    }

    apply(entry.value)
  }
}

function onEntryRemoved()
{
  entry.value = null
  forget(mediaId.value)
}

onMounted(async () =>
{
  try
  {
    await Promise.all([
      loadMedia().catch(() => { media.value = null }),
      loadEntry().catch(() => { entry.value = null })
    ])
  }
  finally
  {
    loading.value = false
  }
})
</script>

<style scoped>
.hero-skeleton { height: 46vh; min-height: 320px; border-radius: var(--ws-radius); }

.back-link { color: var(--ws-red-hot); font-weight: 600; }

/* --- banner --------------------------------------------------------------- */

.banner { position: relative; min-height: 340px; display: flex; align-items: flex-end; }

.banner-image {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center 22%;
}

.banner-wash {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(0deg, var(--ws-void) 2%, rgba(8, 8, 10, 0.72) 45%, rgba(8, 8, 10, 0.5) 100%),
    linear-gradient(90deg, rgba(8, 8, 10, 0.86), rgba(8, 8, 10, 0.2));
}

.banner-inner {
  position: relative;
  display: flex;
  align-items: flex-end;
  gap: 1.6rem;
  width: 100%;
  max-width: 1680px;
  padding: 3.5rem var(--ws-gutter) 1.6rem;
}

.poster {
  width: 168px;
  aspect-ratio: 2 / 3;
  object-fit: cover;
  border-radius: var(--ws-radius);
  box-shadow: var(--ws-shadow);
  flex-shrink: 0;
}

.headline { padding-bottom: 0.35rem; min-width: 0; }

.kinds { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-bottom: 0.7rem; }

.chip.live { color: var(--ws-green); border-color: rgba(62, 207, 142, 0.4); }

.headline h1 { font-size: clamp(1.8rem, 4vw, 3.1rem); font-stretch: 102%; letter-spacing: -0.035em; }

.facts { display: flex; flex-wrap: wrap; gap: 0.45rem; margin-top: 0.55rem; font-size: 0.82rem; color: var(--ws-dim); }

.sep { color: var(--ws-faint); }

.genres { display: flex; flex-wrap: wrap; gap: 0.4rem; margin-top: 0.8rem; }

.genre {
  padding: 0.24rem 0.58rem;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.07);
  font-size: 0.73rem;
  font-weight: 500;
  color: var(--ws-dim);
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease);
}

.genre:hover { background: rgba(255, 255, 255, 0.14); color: var(--ws-text); }

/* --- body ----------------------------------------------------------------- */

.body-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 336px;
  gap: 2.2rem;
  align-items: start;
  padding-top: 1.8rem;
}

.main { min-width: 0; display: grid; gap: 2.2rem; }

.synopsis { font-size: 0.95rem; line-height: 1.75; color: #c8c8d0; max-width: 70ch; white-space: pre-wrap; }

.rail { display: grid; gap: 1rem; position: sticky; top: calc(var(--ws-topbar) + 18px); }

/* --- scorecard ------------------------------------------------------------ */

.scorecard { padding: 1.15rem 1.2rem; display: grid; gap: 1rem; }

.score-lead { display: flex; align-items: center; gap: 0.9rem; }

.score-value { font-size: 2.6rem; font-weight: 600; letter-spacing: -0.04em; line-height: 1; }

.score-side { display: grid; gap: 0.3rem; }

.score-note { font-size: 0.74rem; color: var(--ws-faint); }

.histogram { display: grid; gap: 3px; }

.hist-row { display: grid; grid-template-columns: 16px 1fr 26px; align-items: center; gap: 0.5rem; }

.hist-label { font-size: 0.68rem; color: var(--ws-faint); text-align: right; }

.hist-track { height: 6px; border-radius: 999px; background: rgba(255, 255, 255, 0.07); overflow: hidden; }

.hist-fill { display: block; height: 100%; background: rgba(242, 177, 52, 0.65); border-radius: 999px; }

.hist-count { font-size: 0.68rem; color: var(--ws-faint); }

.elsewhere { display: grid; gap: 0.4rem; padding-top: 0.9rem; border-top: 1px solid var(--ws-line); }

.elsewhere-row { display: grid; grid-template-columns: 1fr auto auto; align-items: baseline; gap: 0.6rem; }

.elsewhere-name { font-size: 0.82rem; color: var(--ws-dim); }

.elsewhere-value { font-size: 0.92rem; font-weight: 600; color: var(--ws-text); }

.elsewhere-votes { font-size: 0.7rem; color: var(--ws-faint); min-width: 42px; text-align: right; }

.counts { display: grid; grid-template-columns: 1fr 1fr; gap: 0.8rem; padding-top: 0.9rem; border-top: 1px solid var(--ws-line); }

.count-value { display: block; font-size: 1.15rem; font-weight: 600; margin-bottom: 0.15rem; }

.signin-prompt { padding: 1.15rem 1.2rem; display: grid; gap: 0.85rem; font-size: 0.86rem; color: var(--ws-dim); }

@media (max-width: 1080px) {
  .body-grid { grid-template-columns: 1fr; }

  .rail { position: static; }
}

@media (max-width: 700px) {
  .banner { min-height: 0; }

  .banner-inner { flex-direction: column; align-items: flex-start; gap: 1rem; padding-top: 2.5rem; }

  .poster { width: 116px; }
}
</style>
