<template>
  <div class="page">
    <header class="page-head">
      <div>
        <span class="eyebrow">Control room</span>
        <h1>Admin</h1>
      </div>
    </header>

    <nav class="tabs" aria-label="Admin sections">
      <button
        v-for="tab in tabs"
        :key="tab"
        type="button"
        class="tab"
        :class="{ on: active === tab }"
        @click="active = tab"
      >{{ tab }}</button>
    </nav>

    <p v-if="banner" class="notice" :class="banner.ok ? 'notice-ok' : 'notice-error'">{{ banner.message }}</p>

    <!-- Overview ------------------------------------------------------------>
    <section v-if="active === 'Overview'" class="stat-band">
      <div class="stat">
        <span class="stat-value num">{{ counts.media }}</span>
        <span class="eyebrow">Titles</span>
      </div>
      <div class="stat">
        <span class="stat-value num">{{ counts.users }}</span>
        <span class="eyebrow">Members</span>
      </div>
      <div class="stat">
        <span class="stat-value num">{{ counts.reviews }}</span>
        <span class="eyebrow">Reviews</span>
      </div>
      <div class="stat">
        <span class="stat-value num">{{ genres.length }}</span>
        <span class="eyebrow">Genres</span>
      </div>
      <div class="stat">
        <span class="stat-value num">{{ counts.episodes }}</span>
        <span class="eyebrow">Episodes tracked</span>
      </div>
    </section>

    <!-- Import -------------------------------------------------------------->
    <section v-if="active === 'Import'" class="stack">
      <div class="panel importer">
        <div class="importer-head">
          <div>
            <span class="eyebrow">Pull from external catalogues</span>
            <h2>Find a title</h2>
          </div>

          <div class="sources">
            <span
              v-for="p in providers"
              :key="p.name"
              class="source"
              :class="{ off: !p.configured }"
              :title="p.configured ? `${p.name} is ready` : `${p.name} has no API key configured`"
            >
              <i class="source-dot"></i>{{ p.name }}
            </span>
          </div>
        </div>

        <p v-if="tmdbMissing" class="notice notice-error">
          TMDB has no API key, so films and TV shows cannot be searched yet. Add a free key from
          themoviedb.org to <code>Tmdb:ApiKey</code> in appsettings.json, or set the
          <code>WATCHSPHERE_TMDB_KEY</code> environment variable, then restart the API.
        </p>

        <form class="import-form" @submit.prevent="runImportSearch">
          <label class="field grow">
            <span class="eyebrow">Title</span>
            <input v-model="importQuery" type="search" placeholder="Frieren, Breaking Bad, Dune…" />
          </label>

          <label class="field">
            <span class="eyebrow">Kind</span>
            <select v-model="importType">
              <option value="">Anything</option>
              <option value="Anime">Anime</option>
              <option value="Movie">Movie</option>
              <option value="Series">Series</option>
            </select>
          </label>

          <button class="btn" type="submit" :disabled="searching || importQuery.trim().length < 2">
            {{ searching ? 'Searching…' : 'Search' }}
          </button>
        </form>
      </div>

      <div class="panel importer">
        <div>
          <span class="eyebrow">Or start from a ranking</span>
          <h2>Fill the catalogue</h2>
        </div>

        <p class="hint">
          Pulls a batch straight in, skipping anything already held. Television
          takes longest, since each season is a separate request.
        </p>

        <form class="import-form" @submit.prevent="seed">
          <label class="field">
            <span class="eyebrow">Kind</span>
            <select v-model="seedType">
              <option value="Anime">Anime</option>
              <option value="Movie">Movies</option>
              <option value="Series">TV shows</option>
            </select>
          </label>

          <label class="field">
            <span class="eyebrow">Ranking</span>
            <select v-model="seedRanking">
              <option value="top">Highest rated</option>
              <option value="trending">Trending this week</option>
            </select>
          </label>

          <label class="field">
            <span class="eyebrow">How many</span>
            <select v-model.number="seedCount">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="40">40</option>
            </select>
          </label>

          <button class="btn" type="submit" :disabled="seeding">
            {{ seeding ? 'Fetching…' : 'Fill' }}
          </button>
        </form>

        <div class="imdb-row">
          <div>
            <span class="eyebrow">IMDb scores</span>
            <p class="hint">
              IMDb publish no API, only a daily dataset. This reads it and writes the
              scores onto every title that carries an IMDb id. Runs nightly too.
            </p>
          </div>

          <button class="btn btn-ghost" type="button" :disabled="imdbBusy" @click="refreshImdb">
            {{ imdbBusy ? 'Reading…' : 'Update scores' }}
          </button>
        </div>

        <div v-if="seedResult" class="seed-result">
          <p class="num">
            <strong>{{ seedResult.imported }}</strong> added ·
            {{ seedResult.skipped }} already held ·
            {{ seedResult.failed }} failed
            <template v-if="seedResult.provider"> · from {{ seedResult.provider }}</template>
          </p>

          <p v-if="seedResult.message" class="hint">{{ seedResult.message }}</p>

          <p v-if="seedResult.titles.length" class="seed-titles">
            {{ seedResult.titles.join(' · ') }}
          </p>
        </div>
      </div>

      <p v-if="searched && importResults.length === 0 && !searching" class="empty">
        <strong>No titles came back.</strong>
        Try the original title, or a shorter part of it.
      </p>

      <div v-if="importResults.length" class="found">
        <article v-for="item in importResults" :key="item.provider + item.externalId" class="found-row panel">
          <img v-if="item.posterUrl" :src="item.posterUrl" alt="" class="found-poster" loading="lazy" />
          <span v-else class="found-poster found-blank" aria-hidden="true">?</span>

          <div class="found-copy">
            <div class="found-title">
              <strong>{{ item.title }}</strong>
              <span class="tag">{{ item.provider }}</span>
              <span class="tag">{{ item.type }}</span>
            </div>

            <p class="found-meta num">
              {{ item.releaseYear || '—' }}
              <template v-if="item.totalEpisodes"> · {{ item.totalEpisodes }} eps</template>
              <template v-if="item.externalRating"> · {{ item.externalRating }}/10</template>
              <template v-if="item.airingStatus"> · {{ item.airingStatus }}</template>
            </p>

            <p v-if="item.description" class="found-blurb">{{ item.description }}</p>
          </div>

          <div class="found-action">
            <router-link
              v-if="item.existingMediaId"
              :to="`/media/${item.existingMediaId}`"
              class="btn btn-quiet btn-sm"
            >In catalogue</router-link>

            <button
              v-else
              class="btn btn-sm"
              type="button"
              :disabled="importingId === item.provider + item.externalId"
              @click="importOne(item)"
            >
              {{ importingId === item.provider + item.externalId ? 'Importing…' : 'Import' }}
            </button>
          </div>
        </article>
      </div>
    </section>

    <!-- Media --------------------------------------------------------------->
    <section v-if="active === 'Media'" class="stack">
      <form class="panel editor" @submit.prevent="saveMedia">
        <div class="editor-head">
          <h2>{{ form.id ? `Editing “${form.title}”` : 'Add a title' }}</h2>
          <button v-if="form.id" class="btn btn-quiet btn-sm" type="button" @click="resetForm">
            New title instead
          </button>
        </div>

        <div class="fields">
          <label class="field span-2">
            <span class="eyebrow">Title</span>
            <input v-model="form.title" type="text" maxlength="200" required />
          </label>

          <label class="field">
            <span class="eyebrow">Kind</span>
            <select v-model="form.type" required>
              <option value="Anime">Anime</option>
              <option value="Movie">Movie</option>
              <option value="Series">Series</option>
            </select>
          </label>

          <label class="field">
            <span class="eyebrow">Release year</span>
            <input v-model.number="form.releaseYear" type="number" min="1888" max="2100" required />
          </label>

          <label class="field">
            <span class="eyebrow">Airing status</span>
            <select v-model="form.airingStatus">
              <option value="Finished">Finished</option>
              <option value="Airing">Airing</option>
              <option value="Upcoming">Upcoming</option>
            </select>
          </label>

          <label class="field">
            <span class="eyebrow">Episodes</span>
            <input
              v-model.number="form.totalEpisodes"
              type="number"
              min="1"
              max="10000"
              :disabled="form.type === 'Movie'"
              :placeholder="form.type === 'Movie' ? '1' : 'Leave blank if unknown'"
            />
          </label>

          <label class="field">
            <span class="eyebrow">Minutes per episode</span>
            <input v-model.number="form.episodeDuration" type="number" min="1" max="1000" placeholder="24" />
          </label>

          <label class="field">
            <span class="eyebrow">Studio</span>
            <input v-model="form.studio" type="text" maxlength="120" />
          </label>

          <label class="field span-2">
            <span class="eyebrow">Poster image URL</span>
            <input v-model="form.posterUrl" type="url" maxlength="500" required />
          </label>

          <label class="field span-2">
            <span class="eyebrow">Backdrop image URL</span>
            <input v-model="form.backdropUrl" type="url" maxlength="500" placeholder="Wide image for the banner" />
          </label>

          <label class="field span-full">
            <span class="eyebrow">Synopsis</span>
            <textarea v-model="form.description" rows="4" maxlength="4000" required></textarea>
          </label>

          <div class="field span-full">
            <span class="eyebrow">Genres</span>
            <div class="genre-picker">
              <button
                v-for="genre in genres"
                :key="genre.id"
                type="button"
                class="genre-toggle"
                :class="{ on: form.genreIds.includes(genre.id) }"
                :aria-pressed="form.genreIds.includes(genre.id)"
                @click="toggleGenre(genre.id)"
              >{{ genre.name }}</button>
            </div>
          </div>
        </div>

        <div class="editor-foot">
          <button class="btn" type="submit" :disabled="busy">
            {{ form.id ? 'Save changes' : 'Add to catalogue' }}
          </button>
        </div>
      </form>

      <div class="panel table-wrap">
        <div class="table-head">
          <h2>Catalogue</h2>
          <input v-model="mediaSearch" type="search" class="table-search" placeholder="Filter by title" />
        </div>

        <table class="table">
          <thead>
            <tr>
              <th>Title</th>
              <th class="tight">Kind</th>
              <th class="tight">Year</th>
              <th class="tight">Eps</th>
              <th class="tight">Score</th>
              <th class="tight"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in filteredMedia" :key="item.id">
              <td>
                <router-link :to="`/media/${item.id}`" class="cell-title">{{ item.title }}</router-link>
              </td>
              <td class="tight">{{ item.type }}</td>
              <td class="tight num">{{ item.releaseYear }}</td>
              <td class="tight num">{{ item.totalEpisodes ?? '—' }}</td>
              <td class="tight num">{{ item.averageRating > 0 ? item.averageRating.toFixed(1) : '—' }}</td>
              <td class="tight actions-cell">
                <button class="link" type="button" @click="editMedia(item)">Edit</button>
                <button
                  class="link"
                  type="button"
                  :disabled="franchiseId === item.id"
                  title="Import every sequel, prequel and other entry in this franchise"
                  @click="followFranchise(item)"
                >{{ franchiseId === item.id ? 'Following…' : 'Franchise' }}</button>
                <button class="link danger" type="button" @click="deleteMedia(item)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>

        <p v-if="filteredMedia.length === 0" class="table-empty">No titles match that filter.</p>
      </div>
    </section>

    <!-- Genres -------------------------------------------------------------->
    <section v-if="active === 'Genres'" class="stack">
      <form class="panel inline-form" @submit.prevent="addGenre">
        <label class="field grow">
          <span class="eyebrow">New genre</span>
          <input v-model="genreDraft" type="text" maxlength="100" required />
        </label>

        <button class="btn" type="submit" :disabled="busy">Add genre</button>
      </form>

      <div class="panel table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th>Genre</th>
              <th class="tight">Titles</th>
              <th class="tight"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="genre in genres" :key="genre.id">
              <td>{{ genre.name }}</td>
              <td class="tight num">{{ genre.mediaCount }}</td>
              <td class="tight actions-cell">
                <button class="link danger" type="button" @click="deleteGenre(genre)">Delete</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Users --------------------------------------------------------------->
    <section v-if="active === 'Users'" class="panel table-wrap">
      <div class="table-head">
        <h2>Members</h2>
        <input v-model="userSearch" type="search" class="table-search" placeholder="Filter by name or email" />
      </div>

      <table class="table">
        <thead>
          <tr>
            <th>Member</th>
            <th class="tight">Role</th>
            <th class="tight">Titles</th>
            <th class="tight">Reviews</th>
            <th class="tight">Joined</th>
            <th class="tight"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="member in filteredUsers" :key="member.id">
            <td>
              <span class="cell-title">{{ member.username }}</span>
              <span class="cell-sub">{{ member.email }}</span>
            </td>
            <td class="tight">
              <select
                :value="member.role"
                :disabled="member.id === me?.id"
                :title="member.id === me?.id ? 'You cannot change your own role' : ''"
                @change="changeRole(member, $event.target.value)"
              >
                <option value="User">User</option>
                <option value="Admin">Admin</option>
              </select>
            </td>
            <td class="tight num">{{ member.libraryCount }}</td>
            <td class="tight num">{{ member.reviewsCount }}</td>
            <td class="tight num">{{ formatDate(member.createdAt) }}</td>
            <td class="tight actions-cell">
              <button
                v-if="member.id !== me?.id"
                class="link danger"
                type="button"
                @click="deleteUser(member)"
              >Delete</button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <!-- Reviews ------------------------------------------------------------->
    <section v-if="active === 'Reviews'" class="panel table-wrap">
      <div class="table-head">
        <h2>Reviews</h2>
      </div>

      <table class="table">
        <thead>
          <tr>
            <th class="tight">Score</th>
            <th>Review</th>
            <th class="tight">Author</th>
            <th class="tight">Posted</th>
            <th class="tight"><span class="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="review in reviews" :key="review.id">
            <td class="tight num score-cell">{{ review.rating }}</td>
            <td>
              <span class="cell-title">{{ review.mediaTitle }}</span>
              <span class="cell-sub">
                <span v-if="review.containsSpoilers" class="spoiler-tag">Spoilers</span>
                {{ review.reviewText }}
              </span>
            </td>
            <td class="tight">{{ review.username }}</td>
            <td class="tight num">{{ formatDate(review.createdAt) }}</td>
            <td class="tight actions-cell">
              <button class="link danger" type="button" @click="deleteReview(review)">Delete</button>
            </td>
          </tr>
        </tbody>
      </table>

      <p v-if="reviews.length === 0" class="table-empty">No reviews have been written yet.</p>
    </section>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import client, { errorMessage } from '../api/client'
import { currentUser } from '../stores/auth'

const tabs = ['Overview', 'Import', 'Media', 'Genres', 'Users', 'Reviews']

const me = currentUser

const active = ref('Overview')
const busy = ref(false)
const banner = ref(null)

const media = ref([])
const genres = ref([])
const users = ref([])
const reviews = ref([])
const counts = reactive({ media: 0, users: 0, reviews: 0, episodes: 0 })

const providers = ref([])
const importQuery = ref('')
const importType = ref('')
const importResults = ref([])
const importingId = ref(null)
const searching = ref(false)
const searched = ref(false)

const seedType = ref('Anime')
const seedRanking = ref('top')
const seedCount = ref(20)
const seeding = ref(false)
const seedResult = ref(null)
const franchiseId = ref(null)
const imdbBusy = ref(false)

const mediaSearch = ref('')
const userSearch = ref('')
const genreDraft = ref('')

const blank = () => ({
  id: null,
  title: '',
  description: '',
  releaseYear: new Date().getFullYear(),
  type: 'Anime',
  posterUrl: '',
  backdropUrl: '',
  airingStatus: 'Finished',
  totalEpisodes: null,
  episodeDuration: null,
  studio: '',
  genreIds: []
})

const form = reactive(blank())

const tmdbMissing = computed(
  () => providers.value.some(p => p.name === 'tmdb' && !p.configured)
)

const filteredMedia = computed(() =>
{
  const term = mediaSearch.value.trim().toLowerCase()

  return term
    ? media.value.filter(item => item.title.toLowerCase().includes(term))
    : media.value
})

const filteredUsers = computed(() =>
{
  const term = userSearch.value.trim().toLowerCase()

  return term
    ? users.value.filter(
        member => member.username.toLowerCase().includes(term) || member.email.toLowerCase().includes(term))
    : users.value
})

function formatDate(value)
{
  return new Date(value).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: '2-digit' })
}

function say(message, ok = true)
{
  banner.value = { message, ok }

  setTimeout(() => { banner.value = null }, 4000)
}

function resetForm()
{
  Object.assign(form, blank())
}

function toggleGenre(id)
{
  const at = form.genreIds.indexOf(id)

  if (at === -1)
  {
    form.genreIds.push(id)
  }
  else
  {
    form.genreIds.splice(at, 1)
  }
}

async function runImportSearch()
{
  searching.value = true
  searched.value = true

  try
  {
    const response = await client.get('/Import/search', {
      params: {
        query: importQuery.value.trim(),
        type: importType.value || undefined
      }
    })

    importResults.value = response.data
  }
  catch (e)
  {
    importResults.value = []
    say(errorMessage(e, 'The search could not be completed.'), false)
  }
  finally
  {
    searching.value = false
  }
}

async function seed()
{
  seeding.value = true
  seedResult.value = null

  try
  {
    const response = await client.post('/Import/popular', null, {
      params: {
        type: seedType.value,
        count: seedCount.value,
        ranking: seedRanking.value
      },
      // A television batch is one request per season upstream, so this can
      // legitimately run for a couple of minutes.
      timeout: 600000
    })

    seedResult.value = response.data

    await loadMedia()
  }
  catch (e)
  {
    say(errorMessage(e, 'The catalogue could not be filled.'), false)
  }
  finally
  {
    seeding.value = false
  }
}

async function refreshImdb()
{
  imdbBusy.value = true

  try
  {
    const response = await client.post('/Import/imdb-ratings', null, { timeout: 600000 })
    const d = response.data

    say(d.message
      ?? `${d.matched} of ${d.known} titles matched, ${d.updated} scores changed.`,
      !d.message)
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not read the IMDb dataset.'), false)
  }
  finally
  {
    imdbBusy.value = false
  }
}

async function followFranchise(item)
{
  franchiseId.value = item.id

  try
  {
    const response = await client.post(`/Import/${item.id}/franchise`, null, {
      timeout: 600000
    })

    const d = response.data

    say(d.imported > 0
      ? `${d.imported} more from the ${d.title} franchise.`
      : (d.message ?? 'Nothing new in that franchise.'), d.imported > 0 || !d.message)

    await loadMedia()
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not follow that franchise.'), false)
  }
  finally
  {
    franchiseId.value = null
  }
}

async function importOne(item)
{
  importingId.value = item.provider + item.externalId

  try
  {
    const response = await client.post('/Import', null, {
      params: {
        provider: item.provider,
        externalId: item.externalId,
        type: item.type
      }
    })

    // Flip the row in place rather than re-running the search, so the list
    // the user was reading does not jump.
    item.existingMediaId = response.data.id

    say(`${response.data.title} added to the catalogue.`)

    await loadMedia()
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not import that title.'), false)
  }
  finally
  {
    importingId.value = null
  }
}

/** Pulls the full catalogue for the table; the admin list is not paged. */
async function loadMedia()
{
  const response = await client.get('/Media', { params: { pageSize: 100, sortBy: 'title' } })

  media.value = response.data.data
}

async function loadAll()
{
  const [genreRes, statsRes, providerRes] = await Promise.all([
    client.get('/Genre'),
    client.get('/Media/statistics'),
    client.get('/Import/providers'),
    loadMedia()
  ])

  providers.value = providerRes.data

  genres.value = genreRes.data
  counts.media = statsRes.data.totalMedia
  counts.users = statsRes.data.totalUsers
  counts.reviews = statsRes.data.totalReviews
  counts.episodes = statsRes.data.totalEpisodesTracked

  const [userRes, reviewRes] = await Promise.all([
    client.get('/User'),
    client.get('/Review', { params: { pageSize: 100 } })
  ])

  users.value = userRes.data
  reviews.value = reviewRes.data.data
}

async function saveMedia()
{
  busy.value = true

  const body = {
    title: form.title,
    description: form.description,
    releaseYear: form.releaseYear,
    type: form.type,
    posterUrl: form.posterUrl,
    backdropUrl: form.backdropUrl,
    airingStatus: form.airingStatus,
    totalEpisodes: form.type === 'Movie' ? 1 : (form.totalEpisodes || null),
    episodeDuration: form.episodeDuration || null,
    studio: form.studio,
    genreIds: form.genreIds
  }

  try
  {
    if (form.id)
    {
      await client.put(`/Media/${form.id}`, body)
      say('Title updated.')
    }
    else
    {
      await client.post('/Media', body)
      say('Title added to the catalogue.')
    }

    resetForm()

    await loadMedia()
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not save the title.'), false)
  }
  finally
  {
    busy.value = false
  }
}

async function editMedia(item)
{
  // The list projection carries genre names, not ids, so the full record is
  // fetched before the form is filled.
  const response = await client.get(`/Media/${item.id}`)
  const full = response.data

  Object.assign(form, {
    id: full.id,
    title: full.title,
    description: full.description,
    releaseYear: full.releaseYear,
    type: full.type,
    posterUrl: full.posterUrl,
    backdropUrl: full.backdropUrl,
    airingStatus: full.airingStatus,
    totalEpisodes: full.totalEpisodes,
    episodeDuration: full.episodeDuration,
    studio: full.studio,
    genreIds: full.genres.map(genre => genre.id)
  })

  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function deleteMedia(item)
{
  if (!window.confirm(`Delete “${item.title}”? Every review and library entry for it goes too.`))
  {
    return
  }

  await client.delete(`/Media/${item.id}`)

  say('Title deleted.')

  await loadMedia()
}

async function addGenre()
{
  busy.value = true

  try
  {
    await client.post('/Genre', { name: genreDraft.value })

    genreDraft.value = ''
    genres.value = (await client.get('/Genre')).data

    say('Genre added.')
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not add that genre.'), false)
  }
  finally
  {
    busy.value = false
  }
}

async function deleteGenre(genre)
{
  if (!window.confirm(`Delete the ${genre.name} genre? It will be removed from ${genre.mediaCount} titles.`))
  {
    return
  }

  await client.delete(`/Genre/${genre.id}`)

  genres.value = (await client.get('/Genre')).data

  say('Genre deleted.')
}

async function changeRole(member, role)
{
  try
  {
    await client.put(`/User/${member.id}/role`, JSON.stringify(role), {
      headers: { 'Content-Type': 'application/json' }
    })

    member.role = role

    say(`${member.username} is now ${role === 'Admin' ? 'an administrator' : 'a member'}.`)
  }
  catch (e)
  {
    say(errorMessage(e, 'Could not change that role.'), false)
  }
}

async function deleteUser(member)
{
  if (!window.confirm(`Delete ${member.username}? Their reviews and library go with them.`))
  {
    return
  }

  await client.delete(`/User/${member.id}`)

  users.value = users.value.filter(u => u.id !== member.id)

  say('Member deleted.')
}

async function deleteReview(review)
{
  if (!window.confirm('Delete this review?'))
  {
    return
  }

  await client.delete(`/Review/${review.id}`)

  reviews.value = reviews.value.filter(r => r.id !== review.id)

  say('Review deleted.')
}

onMounted(loadAll)
</script>

<style scoped>
.tabs { display: flex; gap: 0.4rem; overflow-x: auto; margin-bottom: 1.5rem; scrollbar-width: none; }

.tabs::-webkit-scrollbar { display: none; }

.tab {
  padding: 0.45rem 0.9rem;
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

.stack { display: grid; gap: 1.2rem; }

/* --- overview ------------------------------------------------------------- */

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

/* --- editor --------------------------------------------------------------- */

.editor { padding: 1.2rem 1.3rem; display: grid; gap: 1.1rem; }

.editor-head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }

.fields { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.8rem; }

.field { display: grid; gap: 0.32rem; min-width: 0; }

.span-2 { grid-column: span 2; }

.span-full { grid-column: 1 / -1; }

.editor-foot { display: flex; justify-content: flex-end; }

.genre-picker { display: flex; flex-wrap: wrap; gap: 5px; }

.genre-toggle {
  padding: 0.3rem 0.65rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 999px;
  background: transparent;
  color: var(--ws-faint);
  font-size: 0.76rem;
  font-weight: 500;
  transition: background 0.14s var(--ws-ease), color 0.14s var(--ws-ease), border-color 0.14s var(--ws-ease);
}

.genre-toggle:hover { color: var(--ws-text); }

.genre-toggle.on { background: rgba(225, 29, 46, 0.16); border-color: rgba(225, 29, 46, 0.45); color: #ff9aa4; }

.inline-form { display: flex; align-items: flex-end; gap: 0.8rem; padding: 1.1rem 1.2rem; }

.grow { flex: 1; }

/* --- import --------------------------------------------------------------- */

.importer { padding: 1.2rem 1.3rem; display: grid; gap: 1rem; }

.importer-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }

.sources { display: flex; gap: 0.45rem; flex-wrap: wrap; }

/* A dot per source, because whether a catalogue is reachable is something the
   admin needs to see before wondering why a search came back empty. */
.source {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.24rem 0.6rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--ws-dim);
}

.source-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--ws-green); }

.source.off { color: var(--ws-faint); }

.source.off .source-dot { background: var(--ws-faint); }

.import-form { display: flex; align-items: flex-end; gap: 0.8rem; flex-wrap: wrap; }

.import-form .field { min-width: 130px; }

.import-form .btn { height: 41px; }

.notice code {
  padding: 0.1rem 0.3rem;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.1);
  font-family: 'IBM Plex Mono', monospace;
  font-size: 0.82em;
}

.found { display: grid; gap: 0.6rem; }

.found-row { display: flex; align-items: center; gap: 1rem; padding: 0.8rem 0.9rem; }

.found-poster {
  width: 46px;
  height: 68px;
  object-fit: cover;
  border-radius: 6px;
  flex-shrink: 0;
  background: var(--ws-card);
}

.found-blank { display: grid; place-items: center; color: var(--ws-faint); font-size: 1.2rem; }

.found-copy { flex: 1; min-width: 0; }

.found-title { display: flex; align-items: center; gap: 0.45rem; flex-wrap: wrap; }

.found-title strong { font-size: 0.92rem; }

.tag {
  padding: 0.1rem 0.4rem;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.08);
  font-size: 0.66rem;
  font-weight: 700;
  font-stretch: 108%;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--ws-dim);
}

.found-meta { margin-top: 0.2rem; font-size: 0.74rem; color: var(--ws-faint); }

.found-blurb {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-top: 0.35rem;
  font-size: 0.8rem;
  line-height: 1.5;
  color: var(--ws-dim);
}

.found-action { flex-shrink: 0; }

@media (max-width: 620px) {
  .found-blurb { display: none; }
}

/* --- tables --------------------------------------------------------------- */

.table-wrap { padding: 1.1rem 1.2rem; overflow-x: auto; }

.table-head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: 0.9rem; }

.table-search { width: min(260px, 50vw); padding: 0.42rem 0.7rem; font-size: 0.82rem; }

.table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }

.table th {
  padding: 0 0.65rem 0.55rem;
  border-bottom: 1px solid var(--ws-line);
  font-size: 0.66rem;
  font-weight: 600;
  font-stretch: 118%;
  letter-spacing: 0.14em;
  text-transform: uppercase;
  color: var(--ws-faint);
  text-align: left;
}

.table td { padding: 0.55rem 0.65rem; border-bottom: 1px solid var(--ws-line); vertical-align: top; }

.table tbody tr:hover { background: rgba(255, 255, 255, 0.025); }

.tight { white-space: nowrap; width: 1%; }

.table select { padding: 0.3rem 0.45rem; font-size: 0.8rem; }

.cell-title { display: block; font-weight: 600; }

.cell-sub {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  max-width: 62ch;
  font-size: 0.78rem;
  color: var(--ws-faint);
}

.spoiler-tag {
  display: inline-block;
  margin-right: 0.35rem;
  padding: 0 0.32rem;
  border-radius: 4px;
  background: rgba(242, 177, 52, 0.16);
  color: var(--ws-amber);
  font-size: 0.66rem;
  font-weight: 700;
}

.score-cell { font-size: 0.95rem; font-weight: 600; color: var(--ws-amber); }

.actions-cell { display: flex; gap: 0.7rem; }

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--ws-faint);
  font-size: 0.78rem;
  font-weight: 600;
  transition: color 0.15s var(--ws-ease);
}

.link:hover { color: var(--ws-text); }

.link.danger:hover { color: #ff6070; }

.table-empty { padding: 1.5rem 0 0.5rem; text-align: center; font-size: 0.85rem; color: var(--ws-dim); }

.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }

@media (max-width: 900px) {
  .fields { grid-template-columns: repeat(2, minmax(0, 1fr)); }

  .span-2 { grid-column: span 2; }
}

@media (max-width: 560px) {
  .fields { grid-template-columns: 1fr; }

  .span-2 { grid-column: span 1; }
}
</style>
