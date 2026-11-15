<template>
  <article class="card">
    <router-link :to="`/media/${mediaId}`" class="frame">
      <img
        v-if="media.posterUrl && !broken"
        :src="media.posterUrl"
        :alt="media.title"
        class="poster"
        loading="lazy"
        @error="broken = true"
      />
      <span v-else class="poster fallback" aria-hidden="true">{{ media.title?.charAt(0) }}</span>

      <span class="veil"></span>

      <span class="badges">
        <span class="badge type">{{ media.type }}</span>
        <span v-if="media.airingStatus === 'Airing'" class="badge airing">
          <i class="pulse"></i>Airing
        </span>
      </span>

      <span v-if="score > 0" class="score num" :class="{ mine: isOwnScore }" :title="scoreLabel">
        {{ score.toFixed(1) }}
      </span>

      <!-- The cuff is the point of the card: how far through this title you
           are, readable without opening anything. -->
      <span v-if="entry" class="cuff">
        <span class="cuff-fill" :style="{ width: `${entry.progressPercent}%` }"></span>
      </span>
    </router-link>

    <div class="body">
      <router-link :to="`/media/${mediaId}`" class="title">{{ media.title }}</router-link>

      <div class="meta">
        <StarRating :value="score" />
        <span class="year num">{{ media.releaseYear }}</span>
      </div>

      <div v-if="entry" class="progress">
        <span class="progress-count num">
          {{ entry.episodesWatched }}<span class="slash">/</span>{{ entry.totalEpisodes ?? '?' }}
        </span>

        <button
          v-if="canAdvance"
          class="bump"
          type="button"
          :disabled="bumping"
          :aria-label="`Mark episode ${entry.episodesWatched + 1} of ${media.title} watched`"
          @click="bump"
        >
          +1
        </button>

        <span v-else class="done">{{ entry.status }}</span>
      </div>
    </div>
  </article>
</template>

<script setup>
import { ref, computed } from 'vue'
import client from '../api/client'
import StarRating from './StarRating.vue'

const props = defineProps({
  media: { type: Object, required: true },
  /** The caller's library entry for this title, when they have one. */
  entry: { type: Object, default: null }
})

const emit = defineEmits(['entry-changed'])

const broken = ref(false)
const bumping = ref(false)

// Library responses carry the entry id in `id` and the title in `mediaId`,
// while catalogue responses use `id` for the title itself.
const mediaId = computed(() => props.media.mediaId ?? props.media.id)

// Catalogue cards carry the community average. Cards drawn from your library
// have no average attached, so they show the score you gave it instead.
const isOwnScore = computed(
  () => !(props.media.averageRating > 0) && (props.entry?.score ?? 0) > 0
)

const score = computed(() =>
  props.media.averageRating > 0 ? props.media.averageRating : (props.entry?.score ?? 0)
)

const scoreLabel = computed(() =>
  isOwnScore.value ? 'Your score' : 'Community score'
)

const canAdvance = computed(() =>
{
  if (!props.entry)
  {
    return false
  }

  const total = props.entry.totalEpisodes

  return total === null || total === undefined || props.entry.episodesWatched < total
})

async function bump()
{
  bumping.value = true

  try
  {
    const response = await client.post(`/UserLibrary/${props.entry.id}/episode`)

    emit('entry-changed', response.data)
  }
  finally
  {
    bumping.value = false
  }
}
</script>

<style scoped>
.card { min-width: 0; }

.frame {
  position: relative;
  display: block;
  aspect-ratio: 2 / 3;
  border-radius: var(--ws-radius);
  overflow: hidden;
  background: var(--ws-card);
  transition: transform 0.22s var(--ws-ease), box-shadow 0.22s var(--ws-ease);
}

.card:hover .frame {
  transform: translateY(-4px);
  box-shadow: var(--ws-shadow);
}

.poster { width: 100%; height: 100%; object-fit: cover; }

.fallback {
  display: grid;
  place-items: center;
  font-size: 2.6rem;
  font-weight: 700;
  color: var(--ws-faint);
  background: var(--ws-card);
}

.veil {
  position: absolute;
  inset: 0;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.5) 0%, transparent 42%);
  opacity: 0.9;
}

.badges { position: absolute; top: 8px; left: 8px; display: flex; flex-direction: column; gap: 5px; }

.badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 0.2rem 0.42rem;
  border-radius: 5px;
  font-size: 0.62rem;
  font-weight: 700;
  font-stretch: 106%;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  backdrop-filter: blur(6px);
}

.type { background: rgba(0, 0, 0, 0.62); color: #e6e6ea; }

.airing { background: rgba(62, 207, 142, 0.18); color: var(--ws-green); }

.pulse {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
  animation: ws-pulse 2s ease-in-out infinite;
}

@keyframes ws-pulse { 50% { opacity: 0.25; } }

.score {
  position: absolute;
  top: 8px;
  right: 8px;
  padding: 0.2rem 0.4rem;
  border-radius: 5px;
  background: rgba(0, 0, 0, 0.7);
  color: var(--ws-amber);
  font-size: 0.7rem;
  font-weight: 600;
  backdrop-filter: blur(6px);
}

.score.mine { color: #fff; background: rgba(225, 29, 46, 0.86); }

.cuff {
  position: absolute;
  left: 0;
  right: 0;
  bottom: 0;
  height: 3px;
  background: rgba(255, 255, 255, 0.16);
}

.cuff-fill {
  display: block;
  height: 100%;
  background: var(--ws-red);
  transition: width 0.32s var(--ws-ease);
}

.body { padding: 0.6rem 0.15rem 0; }

.title {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 0.85rem;
  font-weight: 600;
  line-height: 1.32;
  /* Two lines are always reserved, whether the title fills them or not, so the
     star row sits on the same baseline right across a grid of cards. */
  min-height: calc(2 * 1.32em);
  transition: color 0.15s var(--ws-ease);
}

.title:hover { color: var(--ws-red-hot); }

.meta { display: flex; align-items: center; gap: 0.45rem; margin-top: 0.3rem; }

.year { font-size: 0.71rem; color: var(--ws-faint); }

.progress {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-top: 0.4rem;
}

.progress-count { font-size: 0.74rem; font-weight: 500; color: var(--ws-dim); }

.slash { margin: 0 1px; color: var(--ws-faint); }

.bump {
  padding: 0.14rem 0.42rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 5px;
  background: transparent;
  color: var(--ws-dim);
  font-family: 'IBM Plex Mono', monospace;
  font-size: 0.7rem;
  font-weight: 600;
  transition: background 0.15s var(--ws-ease), color 0.15s var(--ws-ease),
              border-color 0.15s var(--ws-ease);
}

.bump:hover:not(:disabled) {
  background: var(--ws-red);
  border-color: var(--ws-red);
  color: #fff;
}

.bump:disabled { opacity: 0.4; cursor: progress; }

.done { font-size: 0.68rem; font-weight: 600; color: var(--ws-faint); }
</style>
