<template>
  <div class="page auth">
    <form class="card panel" @submit.prevent="submit">
      <span class="eyebrow">Join WatchSphere</span>
      <h1>Create your account</h1>
      <p class="lead">Track episodes, score what you finish, and keep your list in one place.</p>

      <p v-if="error" class="notice notice-error">{{ error }}</p>

      <label class="field">
        <span class="eyebrow">Username</span>
        <input v-model="form.username" type="text" minlength="3" maxlength="50" autocomplete="username" required autofocus />
      </label>

      <label class="field">
        <span class="eyebrow">Email</span>
        <input v-model="form.email" type="email" autocomplete="email" required />
      </label>

      <label class="field">
        <span class="eyebrow">Password</span>
        <input v-model="form.password" type="password" minlength="8" autocomplete="new-password" required />
        <span class="hint">At least 8 characters.</span>
      </label>

      <button class="btn submit" type="submit" :disabled="busy">
        {{ busy ? 'Creating account…' : 'Create account' }}
      </button>

      <p class="alt">
        Already have an account?
        <router-link to="/login" class="alt-link">Sign in</router-link>
      </p>
    </form>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import client, { errorMessage } from '../api/client'
import { signIn } from '../stores/auth'
import { reset as resetLibrary } from '../stores/library'

const router = useRouter()

const busy = ref(false)
const error = ref('')

const form = reactive({ username: '', email: '', password: '' })

async function submit()
{
  busy.value = true
  error.value = ''

  try
  {
    const response = await client.post('/Auth/register', form)

    // Registering signs you straight in - there is nothing to confirm.
    signIn(response.data.token, response.data.user)

    resetLibrary()

    router.push('/')
  }
  catch (e)
  {
    error.value = errorMessage(e, 'Could not create the account.')
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

.card h1 { margin-top: 0.35rem; font-size: 1.65rem; }

.lead { margin-bottom: 0.5rem; font-size: 0.88rem; color: var(--ws-dim); }

.field { display: grid; gap: 0.32rem; }

.hint { font-size: 0.72rem; color: var(--ws-faint); }

.submit { margin-top: 0.5rem; }

.alt { text-align: center; font-size: 0.83rem; color: var(--ws-dim); }

.alt-link { color: var(--ws-red-hot); font-weight: 600; }
</style>
