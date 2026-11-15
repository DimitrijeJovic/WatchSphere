<template>
  <div class="page missing">
    <span class="code num">404</span>
    <h1>There is nothing at this address.</h1>
    <p>The link may be out of date, or the title was removed from the catalogue.</p>

    <div class="actions">
      <router-link to="/" class="btn">Go home</router-link>
      <router-link to="/browse" class="btn btn-ghost">Browse the catalogue</router-link>
    </div>
  </div>
</template>

<style scoped>
.missing {
  display: grid;
  place-content: center;
  justify-items: start;
  gap: 0.6rem;
  min-height: calc(100vh - var(--ws-topbar) - 120px);
  max-width: 46ch;
}

.code { font-size: 3.4rem; font-weight: 600; letter-spacing: -0.05em; color: var(--ws-red); line-height: 1; }

.missing p { color: var(--ws-dim); font-size: 0.92rem; }

.actions { display: flex; flex-wrap: wrap; gap: 0.7rem; margin-top: 1rem; }
</style>
