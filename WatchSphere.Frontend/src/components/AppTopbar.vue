<template>
  <header class="topbar">
    <button class="drawer-toggle" type="button" aria-label="Open navigation" @click="$emit('toggle-nav')">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
        <path d="M4 7h16M4 12h16M4 17h16"/>
      </svg>
    </button>

    <button
      v-if="canGoBack"
      class="back"
      type="button"
      :aria-label="`Back to ${previousLabel}`"
      :title="`Back to ${previousLabel}`"
      @click="goBack"
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M15 6l-6 6 6 6"/>
      </svg>
      <span class="back-label">{{ previousLabel }}</span>
    </button>

    <h1 class="crumb">{{ title }}</h1>

    <div ref="searchRoot" class="search" :class="{ active: showResults }">
      <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
        <circle cx="11" cy="11" r="6.5"/><path d="m16 16 4 4"/>
      </svg>

      <input
        v-model="term"
        type="search"
        placeholder="Search titles"
        aria-label="Search titles"
        @focus="showResults = true"
        @keydown.esc="close"
        @keydown.enter="goToBrowse"
      />

      <div v-if="showResults && term.trim().length > 1" class="results panel">
        <p v-if="searching" class="results-note">Searching…</p>

        <p v-else-if="results.length === 0" class="results-note">
          Nothing matches “{{ term.trim() }}”.
        </p>

        <router-link
          v-for="item in results"
          :key="item.id"
          :to="`/media/${item.id}`"
          class="result"
          @click="close"
        >
          <img :src="item.posterUrl" alt="" class="result-poster" loading="lazy" />

          <span class="result-copy">
            <span class="result-title">{{ item.title }}</span>
            <span class="result-meta num">{{ item.type }} · {{ item.releaseYear }}</span>
          </span>

          <span v-if="item.averageRating > 0" class="result-score num">
            {{ item.averageRating.toFixed(1) }}
          </span>
        </router-link>

        <button v-if="results.length > 0" class="results-all" type="button" @click="goToBrowse">
          See all results for “{{ term.trim() }}”
        </button>
      </div>
    </div>

    <div v-if="user" ref="menuRoot" class="account">
      <button class="account-btn" type="button" :aria-expanded="menuOpen" @click="menuOpen = !menuOpen">
        <img v-if="user.avatarUrl" :src="user.avatarUrl" alt="" class="avatar" />
        <span v-else class="avatar avatar-initial">{{ initial }}</span>

        <span class="account-copy">
          <span class="account-name">{{ user.username }}</span>
          <span class="account-role">{{ user.role }}</span>
        </span>

        <svg class="chevron" :class="{ flipped: menuOpen }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round">
          <path d="m7 10 5 5 5-5"/>
        </svg>
      </button>

      <div v-if="menuOpen" class="menu panel">
        <router-link to="/profile" class="menu-item" @click="menuOpen = false">Profile</router-link>
        <router-link to="/library" class="menu-item" @click="menuOpen = false">My library</router-link>
        <router-link v-if="isAdmin" to="/admin" class="menu-item" @click="menuOpen = false">Admin</router-link>
        <button class="menu-item danger" type="button" @click="logout">Sign out</button>
      </div>
    </div>

    <router-link v-else to="/login" class="btn btn-sm">Sign in</router-link>
  </header>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client from '../api/client'
import { currentUser, isAdmin, signOut } from '../stores/auth'

defineEmits(['toggle-nav'])

const route = useRoute()
const router = useRouter()

const user = currentUser

const term = ref('')
const results = ref([])
const searching = ref(false)
const showResults = ref(false)
const menuOpen = ref(false)
const searchRoot = ref(null)
const menuRoot = ref(null)

// Where the browser would go back to. Read from history state rather than kept
// in a stack of our own, so it stays correct when someone uses the browser's
// own back button or lands here from a bookmark.
const previous = ref(null)

let debounce
let requestId = 0

const initial = computed(() => user.value?.username?.charAt(0).toUpperCase() ?? '?')

const canGoBack = computed(() => previous.value !== null)

const previousLabel = computed(() =>
{
  const path = previous.value

  if (!path)
  {
    return 'Back'
  }

  const matched = router.resolve(path)

  if (matched.path.startsWith('/media/'))
  {
    return 'the title'
  }

  if (matched.path === '/browse' && matched.query.type)
  {
    return { Movie: 'Movies', Series: 'TV Shows', Anime: 'Anime' }[matched.query.type] ?? 'Browse'
  }

  return matched.meta.title ?? 'Back'
})

function readHistory()
{
  previous.value = window.history.state?.back ?? null
}

function goBack()
{
  router.back()
}

const title = computed(() =>
{
  if (route.path === '/browse' && route.query.type)
  {
    return { Movie: 'Movies', Series: 'TV Shows', Anime: 'Anime' }[route.query.type] ?? 'Browse'
  }

  return route.meta.title ?? 'WatchSphere'
})

watch(term, value =>
{
  clearTimeout(debounce)

  if (value.trim().length < 2)
  {
    results.value = []
    searching.value = false

    return
  }

  searching.value = true

  debounce = setTimeout(async () =>
  {
    // Responses can land out of order once someone types quickly, so only the
    // newest request is allowed to write to the list.
    const id = ++requestId

    try
    {
      const response = await client.get('/Media', {
        params: { search: value.trim(), pageSize: 6 }
      })

      if (id === requestId)
      {
        results.value = response.data.data
      }
    }
    catch
    {
      if (id === requestId)
      {
        results.value = []
      }
    }
    finally
    {
      if (id === requestId)
      {
        searching.value = false
      }
    }
  }, 250)
})

watch(() => route.fullPath, () =>
{
  close()
  menuOpen.value = false
  readHistory()
})

function close()
{
  showResults.value = false
}

function goToBrowse()
{
  const search = term.value.trim()

  if (search.length === 0)
  {
    return
  }

  router.push({ path: '/browse', query: { search } })

  close()
}

function logout()
{
  signOut()
  menuOpen.value = false
  router.push('/')
}

function onDocumentClick(event)
{
  if (searchRoot.value && !searchRoot.value.contains(event.target))
  {
    showResults.value = false
  }

  if (menuRoot.value && !menuRoot.value.contains(event.target))
  {
    menuOpen.value = false
  }
}

onMounted(() =>
{
  document.addEventListener('click', onDocumentClick)
  readHistory()
})

onUnmounted(() =>
{
  document.removeEventListener('click', onDocumentClick)
  clearTimeout(debounce)
})
</script>

<style scoped>
.topbar {
  position: sticky;
  top: 0;
  z-index: 50;
  height: var(--ws-topbar);
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0 var(--ws-gutter);
  background: rgba(8, 8, 10, 0.82);
  border-bottom: 1px solid var(--ws-line);
  backdrop-filter: blur(18px);
}

.drawer-toggle {
  display: none;
  width: 36px;
  height: 36px;
  padding: 7px;
  border: 1px solid var(--ws-line-strong);
  border-radius: var(--ws-radius-sm);
  background: transparent;
  color: var(--ws-text);
}

.back {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.36rem 0.7rem 0.36rem 0.5rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 999px;
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.8rem;
  font-weight: 600;
  white-space: nowrap;
  flex-shrink: 0;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.back:hover { background: rgba(255, 255, 255, 0.07); color: var(--ws-text); border-color: var(--ws-dim); }

.back svg { width: 16px; height: 16px; }

.crumb { font-size: 1.15rem; font-weight: 700; white-space: nowrap; }

/* --- search --------------------------------------------------------------- */

.search { position: relative; flex: 1; max-width: 460px; margin: 0 auto; }

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  width: 17px;
  height: 17px;
  transform: translateY(-50%);
  color: var(--ws-faint);
  pointer-events: none;
}

.search input {
  padding: 0.56rem 0.85rem 0.56rem 2.3rem;
  border-radius: 999px;
  background: var(--ws-raise);
  font-size: 0.86rem;
}

.search input::-webkit-search-cancel-button { display: none; }

.results {
  position: absolute;
  top: calc(100% + 10px);
  left: 0;
  right: 0;
  padding: 6px;
  background: var(--ws-raise);
  box-shadow: var(--ws-shadow);
  max-height: 62vh;
  overflow-y: auto;
}

.results-note { padding: 0.9rem; text-align: center; font-size: 0.83rem; color: var(--ws-dim); }

.result {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  padding: 0.5rem;
  border-radius: var(--ws-radius-sm);
  transition: background 0.15s var(--ws-ease);
}

.result:hover { background: rgba(255, 255, 255, 0.06); }

.result-poster {
  width: 34px;
  height: 50px;
  object-fit: cover;
  border-radius: 5px;
  background: var(--ws-card);
  flex-shrink: 0;
}

.result-copy { min-width: 0; flex: 1; }

.result-title {
  display: block;
  font-size: 0.86rem;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.result-meta { display: block; font-size: 0.72rem; color: var(--ws-dim); }

.result-score { font-size: 0.8rem; font-weight: 600; color: var(--ws-amber); }

.results-all {
  width: 100%;
  margin-top: 4px;
  padding: 0.6rem;
  border: 0;
  border-top: 1px solid var(--ws-line);
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.78rem;
  font-weight: 600;
  text-align: left;
  border-radius: 0 0 var(--ws-radius-sm) var(--ws-radius-sm);
}

.results-all:hover { color: var(--ws-text); }

/* --- account -------------------------------------------------------------- */

.account { position: relative; flex-shrink: 0; }

.account-btn {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.3rem 0.5rem 0.3rem 0.3rem;
  border: 1px solid transparent;
  border-radius: 999px;
  background: transparent;
  transition: background 0.16s var(--ws-ease);
}

.account-btn:hover { background: rgba(255, 255, 255, 0.06); }

.avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--ws-card);
}

.avatar-initial {
  display: grid;
  place-items: center;
  background: var(--ws-red);
  color: #fff;
  font-size: 0.85rem;
  font-weight: 700;
}

.account-copy { display: flex; flex-direction: column; line-height: 1.25; text-align: left; }

.account-name { font-size: 0.83rem; font-weight: 600; }

.account-role { font-size: 0.68rem; color: var(--ws-faint); }

.chevron { width: 15px; height: 15px; color: var(--ws-faint); transition: transform 0.2s var(--ws-ease); }

.chevron.flipped { transform: rotate(180deg); }

.menu {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  min-width: 174px;
  padding: 5px;
  background: var(--ws-raise);
  box-shadow: var(--ws-shadow);
  display: grid;
  gap: 1px;
}

.menu-item {
  display: block;
  width: 100%;
  padding: 0.52rem 0.65rem;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.85rem;
  text-align: left;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease);
}

.menu-item:hover { background: rgba(255, 255, 255, 0.07); color: var(--ws-text); }

.menu-item.danger:hover { color: #ff6070; }

@media (max-width: 1000px) {
  .drawer-toggle { display: block; }

  .crumb { display: none; }

  /* The arrow alone is unambiguous once the room for a label runs out. */
  .back-label { display: none; }

  .back { padding: 0.36rem 0.5rem; }
}

@media (max-width: 700px) {
  .account-copy { display: none; }

  .chevron { display: none; }

  .search { max-width: none; }
}
</style>
