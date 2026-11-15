<template>
  <span class="stars" :style="{ '--fill': `${percent}%`, '--size': `${size}px` }" :title="label">
    <span class="stars-track" aria-hidden="true">{{ glyphs }}</span>
    <span class="stars-fill" aria-hidden="true">{{ glyphs }}</span>
    <span class="sr-only">{{ label }}</span>
  </span>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  /** Score on the same ten-point scale the API uses. */
  value: { type: Number, default: 0 },
  size: { type: Number, default: 12 }
})

const glyphs = '★★★★★'

// Two stacked copies of the same string, the top one clipped to the score, so
// a 7.3 shows as three and a half stars rather than rounding away the detail.
const percent = computed(() => Math.max(0, Math.min(100, (props.value / 10) * 100)))

const label = computed(() =>
  props.value > 0 ? `Rated ${props.value.toFixed(1)} out of 10` : 'Not rated yet'
)
</script>

<style scoped>
.stars {
  position: relative;
  display: inline-block;
  font-size: var(--size);
  line-height: 1;
  letter-spacing: 0.08em;
  white-space: nowrap;
}

.stars-track { color: rgba(255, 255, 255, 0.16); }

.stars-fill {
  position: absolute;
  inset: 0;
  width: var(--fill);
  overflow: hidden;
  color: var(--ws-amber);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>
