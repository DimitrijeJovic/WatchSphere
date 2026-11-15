<template>
  <div class="page auth">
    <form class="card panel" @submit.prevent="submit">
      <span class="eyebrow">Welcome back</span>
      <h1>Sign in</h1>
      <p class="lead">Your library, progress and reviews are waiting.</p>

      <p v-if="error" class="notice notice-error">{{ error }}</p>

      <label class="field">
        <span class="eyebrow">Email</span>
        <input v-model="form.email" type="email" autocomplete="email" required autofocus />
      </label>

      <label class="field">
        <span class="eyebrow">Password</span>
        <input v-model="form.password" type="password" autocomplete="current-password" required />
      </label>

      <button class="btn submit" type="submit" :disabled="busy">
        {{ busy ? 'Signing in…' : 'Sign in' }}
      </button>

      <p class="alt">
        No account yet?
        <router-link to="/register" class="alt-link">Create one</router-link>
      </p>
    </form>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import client, { errorMessage } from '../api/client'
import { signIn } from '../stores/auth'
import { reset as resetLibrary } from '../stores/library'

const route = useRoute()
const router = useRouter()

const busy = ref(false)
const error = ref('')

const form = reactive({ email: '', password: '' })

async function submit()
{
  busy.value = true
  error.value = ''

  try
  {
    const response = await client.post('/Auth/login', form)

    signIn(response.data.token, response.data.user)

    // The previous session's list must not leak into the new one.
    resetLibrary()

    router.push(route.query.redirect ?? '/')
  }
  catch (e)
  {
    error.value = e.response?.status === 429
      ? 'Too many attempts. Wait a minute and try again.'
      : errorMessage(e, 'Incorrect email or password.')
  }
  finally
  {
    busy.value = false
  }
}
</script>

<style scoped>
.auth { display: grid; place-items: center; min-height: calc(100vh - var(--ws-topbar) - 100px); }

.card { width: min(400px, 100%); padding: 1.9rem 1.8rem; display: grid; gap: 0.85rem; }

.card h1 { margin-top: 0.35rem; font-size: 1.8rem; }

.lead { margin-bottom: 0.5rem; font-size: 0.88rem; color: var(--ws-dim); }

.field { display: grid; gap: 0.32rem; }

.submit { margin-top: 0.5rem; }

.alt { text-align: center; font-size: 0.83rem; color: var(--ws-dim); }

.alt-link { color: var(--ws-red-hot); font-weight: 600; }
</style>
