import { reactive, computed } from 'vue'
import client, { TOKEN_KEY } from '../api/client'

/**
 * The signed-in user, resolved once and shared. Every view used to call
 * /Auth/me on mount, so a single page load hit the endpoint four or five times
 * and each copy could disagree with the others.
 */
const state = reactive({
    user: null,
    ready: false,
    loading: null
})

export const currentUser = computed(() => state.user)
export const isLoggedIn = computed(() => state.user !== null)
export const isAdmin = computed(() => state.user?.role === 'Admin')
export const authReady = computed(() => state.ready)

/** Resolves the session, reusing an in-flight request if one is running. */
export function loadSession()
{
    if (state.loading)
    {
        return state.loading
    }

    if (!localStorage.getItem(TOKEN_KEY))
    {
        state.user = null
        state.ready = true

        return Promise.resolve(null)
    }

    state.loading = client.get('/Auth/me')
        .then(response =>
        {
            state.user = response.data

            return state.user
        })
        .catch(() =>
        {
            localStorage.removeItem(TOKEN_KEY)
            state.user = null

            return null
        })
        .finally(() =>
        {
            state.ready = true
            state.loading = null
        })

    return state.loading
}

export function signIn(token, user)
{
    localStorage.setItem(TOKEN_KEY, token)

    state.user = user
    state.ready = true
}

export function signOut()
{
    localStorage.removeItem(TOKEN_KEY)

    state.user = null
    state.ready = true
}

export function setUser(user)
{
    state.user = user
}

window.addEventListener('watchsphere:unauthorized', () =>
{
    if (state.user !== null)
    {
        signOut()
    }
})
