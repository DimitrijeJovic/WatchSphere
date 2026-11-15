<template>
  <div class="shell">
    <AppSidebar :open="navOpen" @navigate="navOpen = false" />

    <div
      v-if="navOpen"
      class="scrim"
      aria-hidden="true"
      @click="navOpen = false"
    ></div>

    <div class="frame">
      <AppTopbar @toggle-nav="navOpen = !navOpen" />

      <main>
        <router-view v-slot="{ Component }">
          <component :is="Component" :key="$route.fullPath" />
        </router-view>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import AppSidebar from './components/AppSidebar.vue'
import AppTopbar from './components/AppTopbar.vue'

const route = useRoute()
const navOpen = ref(false)

watch(() => route.fullPath, () => { navOpen.value = false })
</script>

<style scoped>
.shell { min-height: 100vh; }

.frame { margin-left: var(--ws-sidebar); min-height: 100vh; }

main { min-height: calc(100vh - var(--ws-topbar)); }

.scrim {
  position: fixed;
  inset: 0;
  z-index: 55;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(2px);
}

@media (max-width: 1000px) {
  .frame { margin-left: 0; }
}

@media (min-width: 1001px) {
  .scrim { display: none; }
}
</style>
