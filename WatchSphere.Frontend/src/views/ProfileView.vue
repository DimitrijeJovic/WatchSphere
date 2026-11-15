<template>
  <div class="page">
    <header class="identity">
      <span v-if="user?.avatarUrl" class="avatar"><img :src="user.avatarUrl" alt="" /></span>
      <span v-else class="avatar initial">{{ user?.username?.charAt(0).toUpperCase() }}</span>

      <div class="identity-copy">
        <span class="eyebrow">{{ user?.role === 'Admin' ? 'Administrator' : 'Member' }}</span>
        <h1>{{ user?.username }}</h1>
        <p class="joined num">Joined {{ joined }}</p>
        <p v-if="user?.bio" class="bio">{{ user.bio }}</p>
      </div>

      <button class="btn btn-ghost" type="button" @click="editing = !editing">
        {{ editing ? 'Close' : 'Edit profile' }}
      </button>
    </header>

    <form v-if="editing" class="editor panel" @submit.prevent="saveProfile">
      <p v-if="saveState" class="notice" :class="saveState.ok ? 'notice-ok' : 'notice-error'">
        {{ saveState.message }}
      </p>

      <label class="field">
        <span class="eyebrow">Avatar image URL</span>
        <input v-model="draft.avatarUrl" type="url" placeholder="https://…" />
      </label>

      <label class="field">
        <span class="eyebrow">Bio</span>
        <textarea v-model="draft.bio" rows="3" maxlength="500" placeholder="A line about what you watch."></textarea>
      </label>

      <div class="editor-foot">
        <span class="counter num">{{ draft.bio.length }}/500</span>
        <button class="btn" type="submit" :disabled="saving">Save profile</button>
      </div>
    </form>

    <section v-if="stats" class="stat-band">
      <div v-for="stat in headline" :key="stat.label" class="stat">
        <span class="stat-value num">{{ stat.value }}</span>
        <span class="eyebrow">{{ stat.label }}</span>
      </div>
    </section>

    <div class="columns">
      <section v-if="stats" class="panel breakdown">
        <div class="section-head">
          <div>
            <span class="eyebrow">How your list sits</span>
            <h2>Status breakdown</h2>
          </div>
        </div>

        <div class="bars">
          <div v-for="row in statusRows" :key="row.label" class="bar-row">
            <span class="bar-label">{{ row.label }}</span>
            <span class="bar-track">
              <span class="bar-fill" :style="{ width: `${row.percent}%`, background: row.color }"></span>
            </span>
            <span class="bar-value num">{{ row.count }}</span>
          </div>
        </div>
      </section>

      <section v-if="stats && stats.meanScore > 0" class="panel breakdown">
        <div class="section-head">
          <div>
            <span class="eyebrow">Mean score {{ stats.meanScore.toFixed(2) }}</span>
            <h2>How you score</h2>
          </div>
        </div>

        <!-- Vertical columns rather than rows: the shape of someone's scoring
             habit reads better left-to-right across the ten-point scale. -->
        <div class="dist">
          <div v-for="bucket in stats.scoreDistribution" :key="bucket.score" class="dist-col">
            <span class="dist-bar" :style="{ height: `${distHeight(bucket.count)}%` }" :title="`${bucket.count} rated ${bucket.score}`"></span>
            <span class="dist-label num">{{ bucket.score }}</span>
          </div>
        </div>
      </section>
    </div>

    <section v-if="favorites.length" class="section">
      <div class="section-head">
        <div>
          <span class="eyebrow">Kept close</span>
          <h2>Favourites</h2>
        </div>

        <router-link to="/library" class="see-all">See all</router-link>
      </div>

      <div class="poster-grid">
        <MediaCard v-for="entry in favorites" :key="entry.id" :media="entry" :entry="entry" @entry-changed="apply" />
      </div>
    </section>

    <section class="section">
      <div class="section-head">
        <div>
          <span class="eyebrow">In your words</span>
          <h2>Your reviews</h2>
        </div>
      </div>

      <div v-if="reviews.length === 0" class="empty">
        <strong>You have not reviewed anything yet.</strong>
        Open a title you have finished and say what you thought.
      </div>

      <div v-else class="review-list">
        <router-link
          v-for="review in reviews"
          :key="review.id"
          :to="`/media/${review.mediaId}`"
          class="review-row panel"
        >
          <img v-if="review.mediaPosterUrl" :src="review.mediaPosterUrl" alt="" class="review-thumb" loading="lazy" />

          <div class="review-copy">
            <span class="review-title">{{ review.mediaTitle }}</span>
            <p class="review-text">{{ review.reviewText }}</p>
          </div>

          <span class="review-score num">{{ review.rating }}<span class="of">/10</span></span>
        </router-link>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import client, { errorMessage } from '../api/client'
import MediaCard from '../components/MediaCard.vue'
import { currentUser, setUser } from '../stores/auth'
import { entriesByMediaId, load, apply } from '../stores/library'

const user = currentUser

const stats = ref(null)
const reviews = ref([])
const editing = ref(false)
const saving = ref(false)
const saveState = ref(null)

const draft = reactive({ avatarUrl: '', bio: '' })

const joined = computed(() =>
  user.value?.createdAt
    ? new Date(user.value.createdAt).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })
    : ''
)

const favorites = computed(() =>
  Object.values(entriesByMediaId.value).filter(entry => entry.isFavorite).slice(0, 12)
)

const headline = computed(() =>
{
  const hours = Math.round((stats.value.minutesWatched ?? 0) / 60)

  return [
    { label: 'Titles tracked', value: stats.value.total.toLocaleString('en-US') },
    { label: 'Episodes watched', value: stats.value.episodesWatched.toLocaleString('en-US') },
    { label: 'Hours watched', value: hours.toLocaleString('en-US') },
    { label: 'Reviews written', value: reviews.value.length.toLocaleString('en-US') }
  ]
})

const statusRows = computed(() =>
{
  const total = Math.max(1, stats.value.total)

  return [
    { label: 'Watching', count: stats.value.watching, color: 'var(--ws-red)' },
    { label: 'Completed', count: stats.value.completed, color: 'var(--ws-green)' },
    { label: 'Planned', count: stats.value.planned, color: '#5a7fd4' },
    { label: 'On hold', count: stats.value.onHold, color: 'var(--ws-amber)' },
    { label: 'Dropped', count: stats.value.dropped, color: '#6b6b78' }
  ].map(row => ({ ...row, percent: (row.count / total) * 100 }))
})

function distHeight(count)
{
  const peak = Math.max(1, ...stats.value.scoreDistribution.map(bucket => bucket.count))

  return count === 0 ? 2 : (count / peak) * 100
}

async function saveProfile()
{
  saving.value = true
  saveState.value = null

  try
  {
    const response = await client.put('/Auth/me', {
      avatarUrl: draft.avatarUrl,
      bio: draft.bio
    })

    setUser(response.data)

    saveState.value = { ok: true, message: 'Profile saved.' }
  }
  catch (e)
  {
    saveState.value = { ok: false, message: errorMessage(e, 'Could not save your profile.') }
  }
  finally
  {
    saving.value = false
  }
}

onMounted(async () =>
{
  draft.avatarUrl = user.value?.avatarUrl ?? ''
  draft.bio = user.value?.bio ?? ''

  const [statsRes, reviewsRes] = await Promise.all([
    client.get('/UserLibrary/me/stats'),
    client.get(`/Review/user/${user.value.id}`),
    load(true)
  ])

  stats.value = statsRes.data
  reviews.value = reviewsRes.data
})
</script>

<style scoped>
.identity { display: flex; align-items: center; gap: 1.2rem; margin-bottom: 2rem; }

.avatar {
  width: 84px;
  height: 84px;
  border-radius: 50%;
  overflow: hidden;
  flex-shrink: 0;
  background: var(--ws-card);
}

.avatar img { width: 100%; height: 100%; object-fit: cover; }

.initial {
  display: grid;
  place-items: center;
  background: var(--ws-red);
  color: #fff;
  font-size: 2rem;
  font-weight: 700;
}

.identity-copy { min-width: 0; flex: 1; }

.identity-copy h1 { margin: 0.3rem 0 0.25rem; }

.joined { font-size: 0.78rem; color: var(--ws-faint); }

.bio { margin-top: 0.55rem; max-width: 60ch; font-size: 0.88rem; color: var(--ws-dim); }

.editor { padding: 1.15rem 1.2rem; display: grid; gap: 0.9rem; margin-bottom: 2rem; }

.field { display: grid; gap: 0.32rem; }

.editor-foot { display: flex; align-items: center; gap: 1rem; }

.counter { font-size: 0.72rem; color: var(--ws-faint); }

.editor-foot .btn { margin-left: auto; }

/* --- stats ---------------------------------------------------------------- */

.stat-band {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 1px;
  background: var(--ws-line);
  border: 1px solid var(--ws-line);
  border-radius: var(--ws-radius);
  overflow: hidden;
}

.stat { padding: 1.15rem 1.3rem; background: var(--ws-panel); }

.stat-value { display: block; margin-bottom: 0.3rem; font-size: 1.5rem; font-weight: 600; letter-spacing: -0.02em; }

.columns { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 1rem; margin-top: 1rem; }

.breakdown { padding: 1.15rem 1.2rem; }

.breakdown .section-head { margin-bottom: 1rem; }

.bars { display: grid; gap: 0.55rem; }

.bar-row { display: grid; grid-template-columns: 84px 1fr 34px; align-items: center; gap: 0.7rem; }

.bar-label { font-size: 0.8rem; color: var(--ws-dim); }

.bar-track { height: 7px; border-radius: 999px; background: rgba(255, 255, 255, 0.07); overflow: hidden; }

.bar-fill { display: block; height: 100%; border-radius: 999px; transition: width 0.35s var(--ws-ease); }

.bar-value { font-size: 0.78rem; color: var(--ws-dim); text-align: right; }

.dist { display: flex; align-items: flex-end; gap: 5px; height: 118px; }

.dist-col { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: flex-end; height: 100%; gap: 0.4rem; }

.dist-bar {
  width: 100%;
  border-radius: 4px 4px 2px 2px;
  background: linear-gradient(180deg, var(--ws-red) 0%, rgba(225, 29, 46, 0.42) 100%);
  transition: height 0.35s var(--ws-ease);
}

.dist-label { font-size: 0.66rem; color: var(--ws-faint); }

.see-all { font-size: 0.76rem; font-weight: 600; color: var(--ws-dim); }

.see-all:hover { color: var(--ws-text); }

/* --- reviews -------------------------------------------------------------- */

.review-list { display: grid; gap: 0.6rem; }

.review-row {
  display: flex;
  align-items: center;
  gap: 0.9rem;
  padding: 0.75rem 0.9rem;
  transition: background 0.15s var(--ws-ease);
}

.review-row:hover { background: var(--ws-raise); }

.review-thumb { width: 38px; height: 56px; object-fit: cover; border-radius: 5px; flex-shrink: 0; }

.review-copy { min-width: 0; flex: 1; }

.review-title { display: block; font-size: 0.88rem; font-weight: 600; }

.review-text {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-top: 0.2rem;
  font-size: 0.82rem;
  line-height: 1.5;
  color: var(--ws-dim);
}

.review-score { font-size: 1.05rem; font-weight: 600; color: var(--ws-amber); flex-shrink: 0; }

.of { font-size: 0.7rem; color: var(--ws-faint); }

@media (max-width: 620px) {
  .identity { flex-wrap: wrap; }

  .avatar { width: 64px; height: 64px; }

  .initial { font-size: 1.5rem; }
}
</style>
