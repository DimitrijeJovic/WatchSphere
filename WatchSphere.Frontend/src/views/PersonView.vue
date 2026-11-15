<template>
  <div v-if="loading" class="page">
    <div class="skeleton head-skeleton"></div>
  </div>

  <div v-else-if="!person" class="page">
    <div class="empty">
      <strong>No one by that id.</strong>
      People arrive with the titles they worked on, so the page exists once a
      title crediting them has been imported.
    </div>
  </div>

  <div v-else class="page">
    <header class="identity">
      <img v-if="person.photoUrl" :src="person.photoUrl" alt="" class="portrait" />
      <span v-else class="portrait blank" aria-hidden="true">{{ person.name.charAt(0) }}</span>

      <div>
        <span class="eyebrow">{{ roleSummary }}</span>
        <h1>{{ person.name }}</h1>
        <p class="count num">
          {{ person.titles.length }} {{ person.titles.length === 1 ? 'title' : 'titles' }} in the catalogue
        </p>
      </div>
    </header>

    <section v-for="group in grouped" :key="group.role" class="section">
      <div class="section-head">
        <div>
          <span class="eyebrow">{{ group.role === 'Actor' || group.role === 'VoiceActor' ? 'Performed in' : group.label }}</span>
          <h2>{{ group.label }}</h2>
        </div>
      </div>

      <div class="poster-grid">
        <article v-for="credit in group.titles" :key="credit.mediaId + credit.role" class="work">
          <router-link :to="`/media/${credit.mediaId}`" class="work-frame">
            <img v-if="credit.posterUrl" :src="credit.posterUrl" alt="" class="work-poster" loading="lazy" />
            <span v-else class="work-poster blank" aria-hidden="true">{{ credit.title.charAt(0) }}</span>
          </router-link>

          <router-link :to="`/media/${credit.mediaId}`" class="work-title">{{ credit.title }}</router-link>

          <span class="work-meta num">{{ credit.type }} · {{ credit.releaseYear }}</span>

          <span v-if="credit.characterName" class="work-role">
            as {{ cleanCharacter(credit.characterName) }}
          </span>
        </article>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import client from '../api/client'

const route = useRoute()

const loading = ref(true)
const person = ref(null)

const labels = {
  Actor: 'Acting',
  VoiceActor: 'Voice roles',
  Director: 'Directed',
  Writer: 'Written',
  Producer: 'Produced',
  Creator: 'Created'
}

const order = ['Actor', 'VoiceActor', 'Director', 'Creator', 'Writer', 'Producer']

const roleSummary = computed(() =>
{
  if (!person.value)
  {
    return ''
  }

  const roles = [...new Set(person.value.titles.map(t => t.role))]

  return roles.map(r => labels[r] ?? r).join(' · ')
})

const grouped = computed(() =>
{
  if (!person.value)
  {
    return []
  }

  const buckets = new Map()

  for (const credit of person.value.titles)
  {
    if (!buckets.has(credit.role))
    {
      buckets.set(credit.role, [])
    }

    // One title can credit the same person for several dubs; the poster only
    // needs to appear once.
    const list = buckets.get(credit.role)

    if (!list.some(existing => existing.mediaId === credit.mediaId))
    {
      list.push(credit)
    }
  }

  return [...buckets.entries()]
    .map(([role, titles]) => ({ role, label: labels[role] ?? role, titles }))
    .sort((a, b) => rank(a.role) - rank(b.role))
})

function rank(role)
{
  const at = order.indexOf(role)

  return at === -1 ? order.length : at
}

function cleanCharacter(name)
{
  return name.replace(/\s*\(voice\)\s*$/i, '')
}

onMounted(async () =>
{
  try
  {
    const response = await client.get(`/Person/${route.params.id}`)

    person.value = response.data
  }
  catch
  {
    person.value = null
  }
  finally
  {
    loading.value = false
  }
})
</script>

<style scoped>
.head-skeleton { height: 140px; border-radius: var(--ws-radius); }

.identity { display: flex; align-items: center; gap: 1.4rem; margin-bottom: 1rem; }

.portrait {
  width: 118px;
  height: 118px;
  border-radius: 50%;
  object-fit: cover;
  object-position: center 20%;
  background: var(--ws-card);
  flex-shrink: 0;
}

.blank {
  display: grid;
  place-items: center;
  font-size: 2.6rem;
  font-weight: 700;
  color: var(--ws-faint);
}

.identity h1 { margin: 0.3rem 0 0.3rem; }

.count { font-size: 0.8rem; color: var(--ws-faint); }

.work { min-width: 0; display: grid; gap: 0.3rem; }

.work-frame {
  display: block;
  aspect-ratio: 2 / 3;
  border-radius: var(--ws-radius);
  overflow: hidden;
  background: var(--ws-card);
  transition: transform 0.2s var(--ws-ease);
}

.work:hover .work-frame { transform: translateY(-4px); }

.work-poster { width: 100%; height: 100%; object-fit: cover; }

.work-poster.blank { font-size: 2rem; }

.work-title {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-top: 0.3rem;
  font-size: 0.84rem;
  font-weight: 600;
  line-height: 1.32;
}

.work-title:hover { color: var(--ws-red-hot); }

.work-meta { font-size: 0.71rem; color: var(--ws-faint); }

.work-role { font-size: 0.74rem; color: var(--ws-dim); }

@media (max-width: 620px) {
  .identity { gap: 1rem; }

  .portrait { width: 84px; height: 84px; }

  .blank { font-size: 1.8rem; }
}
</style>
