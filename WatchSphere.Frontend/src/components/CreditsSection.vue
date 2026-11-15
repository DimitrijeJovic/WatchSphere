<template>
  <section v-if="hasAnything" class="credits">
    <div class="section-head">
      <div>
        <span class="eyebrow">{{ summary }}</span>
        <h2>Cast &amp; crew</h2>
      </div>
    </div>

    <div v-if="cast.length" class="people">
      <router-link
        v-for="member in cast"
        :key="member.id"
        :to="`/person/${member.personId}`"
        class="person"
      >
        <img
          v-if="portrait(member)"
          :src="portrait(member)"
          alt=""
          class="portrait"
          loading="lazy"
        />
        <span v-else class="portrait blank" aria-hidden="true">
          {{ member.personName.charAt(0) }}
        </span>

        <span class="person-name">{{ member.personName }}</span>

        <span v-if="member.characterName" class="person-role">
          {{ cleanCharacter(member.characterName) }}
        </span>
        <span v-else class="person-role">{{ member.role }}</span>
      </router-link>
    </div>

    <div v-if="crewGroups.length" class="crew">
      <div v-for="group in crewGroups" :key="group.role" class="crew-group">
        <span class="crew-role">{{ group.label }}</span>
        <span class="crew-names">
          <router-link
            v-for="(member, i) in group.people"
            :key="member.id"
            :to="`/person/${member.personId}`"
            class="crew-name"
          >{{ member.personName }}<template v-if="i < group.people.length - 1">, </template></router-link>
        </span>
      </div>
    </div>

    <div v-if="studios.length" class="studios">
      <span class="crew-role">{{ studios.length === 1 ? 'Studio' : 'Studios' }}</span>
      <span class="crew-names">{{ studios.map(s => s.name).join(', ') }}</span>
    </div>
  </section>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import client from '../api/client'

const props = defineProps({
  mediaId: { type: Number, required: true }
})

const cast = ref([])
const crew = ref([])
const studios = ref([])

const crewLabels = {
  Director: 'Director',
  Writer: 'Written by',
  Producer: 'Produced by',
  Creator: 'Created by'
}

const crewOrder = ['Director', 'Creator', 'Writer', 'Producer']

const hasAnything = computed(
  () => cast.value.length > 0 || crew.value.length > 0 || studios.value.length > 0
)

const summary = computed(() =>
{
  const parts = []

  if (cast.value.length)
  {
    parts.push(`${cast.value.length} in the cast`)
  }

  if (crew.value.length)
  {
    parts.push(`${crew.value.length} behind it`)
  }

  return parts.join(' · ') || 'Who made it'
})

const crewGroups = computed(() =>
{
  const buckets = new Map()

  for (const member of crew.value)
  {
    if (!buckets.has(member.role))
    {
      buckets.set(member.role, [])
    }

    // The same name can arrive under two job titles that both fold into one
    // role here, and listing it twice reads as a mistake.
    const list = buckets.get(member.role)

    if (!list.some(existing => existing.personId === member.personId))
    {
      list.push(member)
    }
  }

  return [...buckets.entries()]
    .map(([role, people]) => ({
      role,
      label: crewLabels[role] ?? role,
      people: people.slice(0, 6)
    }))
    .sort((a, b) => rank(a.role) - rank(b.role))
})

function rank(role)
{
  const at = crewOrder.indexOf(role)

  return at === -1 ? crewOrder.length : at
}

/** The character portrait when there is one, otherwise the performer's. */
function portrait(member)
{
  return member.characterImageUrl || member.personPhotoUrl || ''
}

/** TMDB appends "(voice)" to animated roles; the section already says so. */
function cleanCharacter(name)
{
  return name.replace(/\s*\(voice\)\s*$/i, '')
}

async function load()
{
  const response = await client.get(`/Media/${props.mediaId}/credits`)

  cast.value = response.data.cast
  crew.value = response.data.crew
  studios.value = response.data.studios
}

watch(() => props.mediaId, load)

onMounted(load)
</script>

<style scoped>
.credits { display: grid; gap: 1.1rem; }

/* A scrolling strip rather than a grid: the cast is ordered by billing, and a
   wrapping grid hides that the first few names are the ones that matter. */
.people {
  display: flex;
  gap: 0.9rem;
  overflow-x: auto;
  padding-bottom: 6px;
  margin: 0 calc(var(--ws-gutter) * -1);
  padding-left: var(--ws-gutter);
  padding-right: var(--ws-gutter);
  scrollbar-width: none;
}

.people::-webkit-scrollbar { display: none; }

.person { flex: 0 0 104px; display: grid; gap: 0.35rem; }

.portrait {
  width: 104px;
  height: 104px;
  border-radius: 50%;
  object-fit: cover;
  object-position: center 20%;
  background: var(--ws-card);
  transition: transform 0.2s var(--ws-ease);
}

.person:hover .portrait { transform: scale(1.05); }

.blank {
  display: grid;
  place-items: center;
  font-size: 2rem;
  font-weight: 700;
  color: var(--ws-faint);
}

.person-name {
  font-size: 0.8rem;
  font-weight: 600;
  line-height: 1.3;
  color: var(--ws-text);
}

.person-role { font-size: 0.72rem; line-height: 1.35; color: var(--ws-faint); }

/* --- crew ----------------------------------------------------------------- */

.crew { display: grid; gap: 0.5rem; }

.crew-group, .studios { display: grid; grid-template-columns: 116px minmax(0, 1fr); gap: 0.8rem; }

.studios { padding-top: 0.8rem; border-top: 1px solid var(--ws-line); }

.crew-role {
  font-size: 0.7rem;
  font-weight: 600;
  font-stretch: 112%;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--ws-faint);
  padding-top: 0.15rem;
}

.crew-names { font-size: 0.86rem; line-height: 1.55; color: var(--ws-dim); }

.crew-name { transition: color 0.15s var(--ws-ease); }

.crew-name:hover { color: var(--ws-red-hot); }

@media (max-width: 600px) {
  .crew-group, .studios { grid-template-columns: 1fr; gap: 0.15rem; }

  .person { flex-basis: 88px; }

  .portrait { width: 88px; height: 88px; }
}
</style>
