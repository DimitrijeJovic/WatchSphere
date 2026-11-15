<template>
  <aside class="sidebar" :class="{ open }">
    <router-link to="/" class="wordmark" @click="$emit('navigate')">
      <span class="glyph" aria-hidden="true">
        <svg viewBox="0 0 24 24" fill="none"><path d="M9 7.5v9l7.5-4.5L9 7.5Z" fill="currentColor"/></svg>
      </span>
      <span class="wordmark-text">WatchSphere</span>
    </router-link>

    <nav class="nav" aria-label="Main">
      <p class="nav-label eyebrow">Browse</p>

      <router-link
        v-for="link in browseLinks"
        :key="link.label"
        :to="link.to"
        class="nav-item"
        :class="{ current: isCurrent(link) }"
        @click="$emit('navigate')"
      >
        <span class="nav-icon" v-html="link.icon" aria-hidden="true"></span>
        <span>{{ link.label }}</span>
      </router-link>

      <template v-if="isLoggedIn">
        <p class="nav-label eyebrow">You</p>

        <router-link
          v-for="link in personalLinks"
          :key="link.label"
          :to="link.to"
          class="nav-item"
          :class="{ current: isCurrent(link) }"
          @click="$emit('navigate')"
        >
          <span class="nav-icon" v-html="link.icon" aria-hidden="true"></span>
          <span>{{ link.label }}</span>
        </router-link>
      </template>
    </nav>

    <div class="sidebar-foot">
      <router-link v-if="isAdmin" to="/admin" class="nav-item" @click="$emit('navigate')">
        <span class="nav-icon" v-html="icons.admin" aria-hidden="true"></span>
        <span>Admin</span>
      </router-link>

      <router-link v-if="!isLoggedIn" to="/login" class="btn signin-btn" @click="$emit('navigate')">
        Sign in
      </router-link>

      <!-- TMDB's terms require this wherever their data is shown. -->
      <p class="credits">
        Title data from
        <a href="https://www.themoviedb.org/" target="_blank" rel="noopener">TMDB</a>,
        <a href="https://www.tvmaze.com/" target="_blank" rel="noopener">TVmaze</a>
        and
        <a href="https://kitsu.app/" target="_blank" rel="noopener">Kitsu</a>.
        Not endorsed or certified by any of them.
      </p>
    </div>
  </aside>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { isLoggedIn, isAdmin } from '../stores/auth'

defineProps({
  open: Boolean
})

defineEmits(['navigate'])

const route = useRoute()

const icons = {
  home: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M3 10.5 12 3l9 7.5"/><path d="M5.5 9.5V21h13V9.5"/></svg>',
  movie: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 9.5h18M8 5v4.5M16 5v4.5"/></svg>',
  series: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><rect x="2.5" y="7.5" width="19" height="12.5" rx="2"/><path d="m8 4 4 3.5L16 4"/></svg>',
  anime: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><path d="M12 3c3 3.5 3 14.5 0 18M3.2 10.5h17.6M3.2 13.5h17.6"/></svg>',
  library: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M5 4h4v16H5zM11 4h3v16h-3z"/><path d="m17.2 4.6 3 15.1"/></svg>',
  profile: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8.5" r="3.6"/><path d="M4.8 20a7.4 7.4 0 0 1 14.4 0"/></svg>',
  admin: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3 4.5 6.2v5.4c0 4.4 3.1 8.3 7.5 9.4 4.4-1.1 7.5-5 7.5-9.4V6.2z"/></svg>'
}

const browseLinks = [
  { label: 'Home', to: '/', icon: icons.home },
  { label: 'Movies', to: '/browse?type=Movie', type: 'Movie', icon: icons.movie },
  { label: 'TV Shows', to: '/browse?type=Series', type: 'Series', icon: icons.series },
  { label: 'Anime', to: '/browse?type=Anime', type: 'Anime', icon: icons.anime }
]

const personalLinks = [
  { label: 'My Library', to: '/library', icon: icons.library },
  { label: 'Profile', to: '/profile', icon: icons.profile }
]

/**
 * The three browse links share one route and differ only by query, so an
 * exact-path match would light all of them at once.
 */
function isCurrent(link)
{
  const path = link.to.split('?')[0]

  if (path !== route.path)
  {
    return false
  }

  if (link.type)
  {
    return route.query.type === link.type
  }

  return route.path !== '/browse' || !route.query.type
}
</script>

<style scoped>
.sidebar {
  position: fixed;
  inset: 0 auto 0 0;
  z-index: 60;
  width: var(--ws-sidebar);
  display: flex;
  flex-direction: column;
  padding: 0 14px 18px;
  background: var(--ws-panel);
  border-right: 1px solid var(--ws-line);
}

.wordmark {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  height: var(--ws-topbar);
  padding-left: 6px;
  flex-shrink: 0;
}

.glyph {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border-radius: 9px;
  background: var(--ws-red);
  color: #fff;
  box-shadow: 0 0 0 4px rgba(225, 29, 46, 0.13);
}

.glyph svg { width: 22px; height: 22px; }

.wordmark-text {
  font-size: 1.02rem;
  font-weight: 700;
  font-stretch: 108%;
  letter-spacing: -0.015em;
}

.nav { display: flex; flex-direction: column; gap: 2px; margin-top: 0.9rem; overflow-y: auto; }

.nav-label { margin: 1.1rem 0 0.45rem 12px; }

.nav-label:first-child { margin-top: 0; }

.nav-item {
  position: relative;
  display: flex;
  align-items: center;
  gap: 0.72rem;
  padding: 0.6rem 0.75rem;
  border-radius: var(--ws-radius-sm);
  color: var(--ws-dim);
  font-size: 0.88rem;
  font-weight: 500;
  transition: color 0.16s var(--ws-ease), background 0.16s var(--ws-ease);
}

.nav-item:hover { color: var(--ws-text); background: rgba(255, 255, 255, 0.05); }

.nav-item.current { color: #fff; background: var(--ws-red); }

.nav-item.current:hover { background: var(--ws-red-hot); }

.nav-icon { display: block; width: 19px; height: 19px; flex-shrink: 0; }

.nav-icon :deep(svg) { width: 100%; height: 100%; display: block; }

.sidebar-foot { margin-top: auto; padding-top: 1rem; display: grid; gap: 2px; }

.signin-btn { margin-top: 0.5rem; }

.credits {
  margin-top: 1rem;
  padding: 0 0.75rem;
  font-size: 0.66rem;
  line-height: 1.5;
  color: #4a4a54;
}

.credits a { color: var(--ws-faint); text-decoration: underline; text-underline-offset: 2px; }

.credits a:hover { color: var(--ws-dim); }

/* Below the desktop breakpoint the sidebar becomes a drawer the topbar opens. */
@media (max-width: 1000px) {
  .sidebar {
    transform: translateX(-100%);
    transition: transform 0.24s var(--ws-ease);
    box-shadow: var(--ws-shadow);
  }

  .sidebar.open { transform: none; }
}
</style>
