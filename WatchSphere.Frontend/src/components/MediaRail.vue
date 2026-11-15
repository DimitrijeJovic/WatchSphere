<template>
  <section class="rail-section">
    <div class="section-head">
      <div>
        <span v-if="eyebrow" class="eyebrow">{{ eyebrow }}</span>
        <h2>{{ title }}</h2>
      </div>

      <router-link v-if="to" :to="to" class="see-all">See all</router-link>
    </div>

    <div class="rail-wrap">
      <button
        v-show="canScrollBack"
        class="arrow back"
        type="button"
        aria-label="Scroll back"
        @click="scrollBy(-1)"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="m14 6-6 6 6 6"/></svg>
      </button>

      <div ref="track" class="rail" @scroll.passive="updateArrows">
        <template v-if="loading">
          <div v-for="n in 8" :key="n" class="rail-item">
            <div class="skeleton skeleton-poster"></div>
          </div>
        </template>

        <template v-else>
          <div v-for="item in items" :key="item.mediaId ?? item.id" class="rail-item">
            <slot :item="item">
              <MediaCard :media="item" :entry="entryFor(item)" @entry-changed="$emit('entry-changed', $event)" />
            </slot>
          </div>
        </template>
      </div>

      <button
        v-show="canScrollForward"
        class="arrow forward"
        type="button"
        aria-label="Scroll forward"
        @click="scrollBy(1)"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="m10 6 6 6-6 6"/></svg>
      </button>
    </div>
  </section>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted, nextTick } from 'vue'
import MediaCard from './MediaCard.vue'

const props = defineProps({
  title: { type: String, required: true },
  eyebrow: { type: String, default: '' },
  items: { type: Array, default: () => [] },
  /** Library entries keyed by media id, so rails can show progress. */
  entries: { type: Object, default: () => ({}) },
  to: { type: String, default: '' },
  loading: Boolean
})

defineEmits(['entry-changed'])

const track = ref(null)
const canScrollBack = ref(false)
const canScrollForward = ref(false)

function entryFor(item)
{
  return props.entries[item.mediaId ?? item.id] ?? null
}

function updateArrows()
{
  const el = track.value

  if (!el)
  {
    return
  }

  canScrollBack.value = el.scrollLeft > 8
  canScrollForward.value = el.scrollLeft + el.clientWidth < el.scrollWidth - 8
}

/** Pages by just under a full viewport so a partial card stays as an anchor. */
function scrollBy(direction)
{
  const el = track.value

  if (el)
  {
    el.scrollBy({ left: direction * el.clientWidth * 0.86, behavior: 'smooth' })
  }
}

watch(() => props.items, () => nextTick(updateArrows), { deep: true })
watch(() => props.loading, () => nextTick(updateArrows))

onMounted(() =>
{
  nextTick(updateArrows)
  window.addEventListener('resize', updateArrows)
})

onUnmounted(() => window.removeEventListener('resize', updateArrows))
</script>

<style scoped>
.rail-section { margin-top: 2.4rem; }

.see-all {
  font-size: 0.76rem;
  font-weight: 600;
  color: var(--ws-dim);
  transition: color 0.15s var(--ws-ease);
}

.see-all:hover { color: var(--ws-text); }

.rail-wrap { position: relative; }

/* The rail bleeds into the page gutter so cards run off the edge the way they
   do on the reference, rather than stopping at a hard margin. */
.rail {
  display: flex;
  gap: 1rem;
  overflow-x: auto;
  scroll-snap-type: x proximity;
  scrollbar-width: none;
  padding: 0 var(--ws-gutter) 4px;
  margin: 0 calc(var(--ws-gutter) * -1);
}

.rail::-webkit-scrollbar { display: none; }

.rail-item { flex: 0 0 162px; scroll-snap-align: start; }

.skeleton-poster { aspect-ratio: 2 / 3; border-radius: var(--ws-radius); }

.arrow {
  position: absolute;
  top: 0;
  bottom: 34px;
  z-index: 4;
  width: 44px;
  display: grid;
  place-items: center;
  border: 0;
  background: linear-gradient(to right, rgba(8, 8, 10, 0.94), rgba(8, 8, 10, 0));
  color: var(--ws-text);
  opacity: 0;
  transition: opacity 0.18s var(--ws-ease);
}

.arrow svg { width: 26px; height: 26px; }

.arrow.back { left: calc(var(--ws-gutter) * -1); border-radius: var(--ws-radius) 0 0 var(--ws-radius); }

.arrow.forward {
  right: calc(var(--ws-gutter) * -1);
  background: linear-gradient(to left, rgba(8, 8, 10, 0.94), rgba(8, 8, 10, 0));
  border-radius: 0 var(--ws-radius) var(--ws-radius) 0;
}

.rail-wrap:hover .arrow, .arrow:focus-visible { opacity: 1; }

@media (max-width: 700px) {
  .rail-item { flex-basis: 132px; }

  .arrow { display: none; }
}
</style>
