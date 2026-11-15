<template>
  <section class="spotlight">
    <div
      class="hero"
      @mouseenter="paused = true"
      @mouseleave="paused = false"
      @focusin="paused = true"
      @focusout="paused = false"
    >
      <template v-if="loading">
        <div class="skeleton hero-skeleton"></div>
      </template>

      <template v-else-if="active">
        <img
          v-if="backdrop"
          :key="backdrop"
          :src="backdrop"
          alt=""
          class="hero-image"
          @error="onImageError"
        />

        <div class="hero-wash"></div>

        <div class="hero-copy">
          <span class="eyebrow">{{ eyebrow }}</span>

          <h2 class="hero-title">{{ active.title }}</h2>

          <div class="hero-facts">
            <StarRating :value="active.averageRating ?? 0" :size="13" />
            <span v-if="active.averageRating > 0" class="num">{{ active.averageRating.toFixed(1) }}</span>
            <span class="dot">·</span>
            <span class="num">{{ active.releaseYear }}</span>
            <span v-if="episodeLabel" class="dot">·</span>
            <span v-if="episodeLabel" class="num">{{ episodeLabel }}</span>
          </div>

          <p v-if="active.description" class="hero-blurb">{{ active.description }}</p>

          <div class="hero-actions">
            <router-link :to="`/media/${active.id}`" class="btn">View title</router-link>

            <span v-if="active.genres?.length" class="hero-genres">
              {{ active.genres.slice(0, 3).join(' · ') }}
            </span>
          </div>
        </div>

        <div v-if="items.length > 1" class="dots" role="tablist" aria-label="Featured titles">
          <button
            v-for="(item, i) in items"
            :key="item.id"
            class="dot-btn"
            :class="{ on: i === index }"
            type="button"
            role="tab"
            :aria-selected="i === index"
            :aria-label="item.title"
            @click="select(i)"
          ></button>
        </div>
      </template>
    </div>

    <div class="side">
      <template v-if="loading">
        <div v-for="n in 8" :key="n" class="skeleton side-skeleton"></div>
      </template>

      <template v-else>
        <MediaCard
          v-for="item in side"
          :key="item.id"
          :media="item"
          :entry="entries[item.id] ?? null"
          @entry-changed="$emit('entry-changed', $event)"
        />
      </template>
    </div>
  </section>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import MediaCard from './MediaCard.vue'
import StarRating from './StarRating.vue'

const props = defineProps({
  items: { type: Array, default: () => [] },
  side: { type: Array, default: () => [] },
  entries: { type: Object, default: () => ({}) },
  eyebrow: { type: String, default: 'In the spotlight' },
  loading: Boolean
})

defineEmits(['entry-changed'])

const index = ref(0)
const paused = ref(false)
const fellBack = ref(false)

let timer

const active = computed(() => props.items[index.value] ?? null)

// Backdrops are optional in the catalogue, so the poster stands in when one is
// missing or fails to load.
const backdrop = computed(() =>
{
  if (!active.value)
  {
    return ''
  }

  if (!fellBack.value && active.value.backdropUrl)
  {
    return active.value.backdropUrl
  }

  return active.value.posterUrl
})

const episodeLabel = computed(() =>
{
  const total = active.value?.totalEpisodes

  if (!total || active.value.type === 'Movie')
  {
    return ''
  }

  return `${total} eps`
})

function onImageError()
{
  fellBack.value = true
}

function select(i)
{
  index.value = i
  restart()
}

function restart()
{
  clearInterval(timer)

  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (reduced || props.items.length < 2)
  {
    return
  }

  timer = setInterval(() =>
  {
    if (!paused.value)
    {
      index.value = (index.value + 1) % props.items.length
    }
  }, 7000)
}

watch(() => props.items, () =>
{
  index.value = 0
  fellBack.value = false
  restart()
})

watch(index, () => { fellBack.value = false })

onMounted(restart)

onUnmounted(() => clearInterval(timer))
</script>

<style scoped>
/* The hero is as tall as two rows of posters, with eight cards filling the
   space beside it. One row left the lower half of that block empty. */
.spotlight {
  display: grid;
  grid-template-columns: 2.6fr repeat(4, 1fr);
  grid-auto-rows: min-content;
  gap: 1rem;
  align-items: start;
}

.hero {
  position: relative;
  grid-row: span 2;
  align-self: stretch;
  min-height: 420px;
  border-radius: var(--ws-radius);
  overflow: hidden;
  /* Its own stacking context, so the scaling image inside is composited
     against this box rather than re-clipped against the page on every frame. */
  isolation: isolate;
  background: var(--ws-card);
}

.hero-skeleton, .side-skeleton { width: 100%; height: 100%; border-radius: var(--ws-radius); }

.side-skeleton { aspect-ratio: 2 / 3; }

.hero-image {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;

  /* Promoted to its own compositor layer, otherwise the browser repaints a
     very large bitmap every frame and the drift visibly steps. */
  will-change: transform;
  backface-visibility: hidden;

  /* Breathes in and back out rather than drifting one way and stopping. The
     earlier long, slow version was the problem: spread over twenty seconds it
     moved a fraction of a pixel per frame, and that is what showed as
     stepping. Three seconds each way covers real pixels every frame. */
  animation: ws-breathe 6s ease-in-out infinite;
}

@keyframes ws-breathe {
  0% { transform: scale(1) translate3d(0, 0, 0); }
  50% { transform: scale(1.06) translate3d(0, 0, 0); }
  100% { transform: scale(1) translate3d(0, 0, 0); }
}

.hero-wash {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(90deg, rgba(8, 8, 10, 0.94) 0%, rgba(8, 8, 10, 0.62) 46%, rgba(8, 8, 10, 0.1) 100%),
    linear-gradient(0deg, rgba(8, 8, 10, 0.9) 0%, transparent 55%);
}

.hero-copy {
  position: absolute;
  inset: auto 0 0 0;
  padding: 1.6rem 1.7rem 2.4rem;
  max-width: 78%;
}

.hero-title {
  margin: 0.5rem 0 0.55rem;
  font-size: clamp(1.5rem, 2.6vw, 2.5rem);
  font-weight: 800;
  font-stretch: 104%;
  letter-spacing: -0.03em;
  line-height: 1.02;
}

.hero-facts {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.78rem;
  color: var(--ws-dim);
}

.dot { color: var(--ws-faint); }

.hero-blurb {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-top: 0.7rem;
  max-width: 46ch;
  font-size: 0.86rem;
  line-height: 1.6;
  color: #c3c3cc;
}

.hero-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 0.9rem; margin-top: 1.1rem; }

.hero-genres { font-size: 0.74rem; font-weight: 500; color: var(--ws-faint); }

.dots { position: absolute; left: 1.7rem; bottom: 1.2rem; display: flex; gap: 6px; }

.dot-btn {
  width: 22px;
  height: 3px;
  padding: 0;
  border: 0;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.26);
  transition: background 0.2s var(--ws-ease), width 0.2s var(--ws-ease);
}

.dot-btn.on { width: 34px; background: var(--ws-red); }

.side { display: contents; }

/* Fewer columns as the window narrows, so the trailing cards drop out in
   pairs and both rows stay full. */
@media (max-width: 1400px) {
  .spotlight { grid-template-columns: 2.2fr repeat(3, 1fr); }

  .side > :nth-child(n + 7) { display: none; }
}

@media (max-width: 1100px) {
  .spotlight { grid-template-columns: 1.9fr repeat(2, 1fr); }

  .side > :nth-child(n + 5) { display: none; }
}

@media (max-width: 820px) {
  .spotlight { grid-template-columns: 1fr; }

  .hero { grid-row: auto; aspect-ratio: 16 / 11; min-height: 0; }

  .hero-copy { max-width: 100%; padding: 1.2rem 1.2rem 2rem; }

  .side { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0.8rem; }

  .side > :nth-child(n) { display: block; }

  .side > :nth-child(n + 9) { display: none; }
}

@media (max-width: 520px) {
  .side { grid-template-columns: repeat(3, 1fr); }

  .side > :nth-child(n + 7) { display: none; }
}
</style>
