<template>
  <section v-if="groups.length" class="related">
    <div class="section-head">
      <div>
        <span class="eyebrow">{{ total }} more in this franchise</span>
        <h2>Related titles</h2>
      </div>
    </div>

    <div v-for="group in groups" :key="group.type" class="group">
      <p class="group-label">{{ label(group.type) }}</p>

      <div class="poster-grid">
        <MediaCard
          v-for="item in group.items"
          :key="item.id"
          :media="item"
          :entry="entries[item.id] ?? null"
          @entry-changed="apply"
        />
      </div>
    </div>
  </section>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import client from '../api/client'
import MediaCard from './MediaCard.vue'
import { entriesByMediaId, apply } from '../stores/library'

const props = defineProps({
  mediaId: { type: Number, required: true }
})

const items = ref([])
const entries = entriesByMediaId

// Sequels and prequels are the ones people actually go looking for, so they
// lead; the odd promotional short belongs at the bottom.
const order = [
  'Prequel',
  'Sequel',
  'ParentStory',
  'SideStory',
  'Spinoff',
  'Collection',
  'AlternativeVersion',
  'Adaptation',
  'FullStory',
  'Summary',
  'Other'
]

const labels = {
  Prequel: 'Comes before',
  Sequel: 'Comes after',
  ParentStory: 'Main story',
  SideStory: 'Side stories',
  Spinoff: 'Spin-offs',
  Collection: 'The rest of the series',
  AlternativeVersion: 'Other versions',
  Adaptation: 'Adaptations',
  FullStory: 'Full story',
  Summary: 'Recaps',
  Other: 'Also related'
}

const total = computed(() => items.value.length)

const groups = computed(() =>
{
  const buckets = new Map()

  for (const item of items.value)
  {
    if (!buckets.has(item.relationType))
    {
      buckets.set(item.relationType, [])
    }

    buckets.get(item.relationType).push(item)
  }

  return [...buckets.entries()]
    .map(([type, list]) => ({ type, items: list }))
    .sort((a, b) => rank(a.type) - rank(b.type))
})

function rank(type)
{
  const at = order.indexOf(type)

  return at === -1 ? order.length : at
}

function label(type)
{
  return labels[type] ?? type
}

async function load()
{
  const response = await client.get(`/Media/${props.mediaId}/related`)

  items.value = response.data
}

watch(() => props.mediaId, load)

onMounted(load)
</script>

<style scoped>
.related { display: grid; gap: 1.4rem; }

.group { display: grid; gap: 0.7rem; }

.group-label {
  font-size: 0.72rem;
  font-weight: 600;
  font-stretch: 112%;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--ws-faint);
}
</style>
