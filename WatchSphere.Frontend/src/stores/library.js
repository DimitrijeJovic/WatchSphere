import { reactive, computed } from 'vue'
import client from '../api/client'
import { isLoggedIn } from './auth'

/**
 * The caller's library, keyed by media id. Catalogue endpoints return the same
 * cards to everyone, so this is what lets a poster anywhere in the app show
 * your own progress without a per-card request.
 */
const state = reactive({
    byMediaId: {},
    loaded: false,
    loading: null
})

export const entriesByMediaId = computed(() => state.byMediaId)

export function load(force = false)
{
    if (!isLoggedIn.value)
    {
        state.byMediaId = {}
        state.loaded = true

        return Promise.resolve({})
    }

    if (state.loaded && !force)
    {
        return Promise.resolve(state.byMediaId)
    }

    if (state.loading)
    {
        return state.loading
    }

    state.loading = client.get('/UserLibrary/me')
        .then(response =>
        {
            const map = {}

            for (const entry of response.data)
            {
                map[entry.mediaId] = entry
            }

            state.byMediaId = map
            state.loaded = true

            return map
        })
        .catch(() => state.byMediaId)
        .finally(() => { state.loading = null })

    return state.loading
}

/** Writes back a single entry after an update, so every card re-renders. */
export function apply(entry)
{
    if (entry)
    {
        state.byMediaId = { ...state.byMediaId, [entry.mediaId]: entry }
    }
}

export function forget(mediaId)
{
    const next = { ...state.byMediaId }

    delete next[mediaId]

    state.byMediaId = next
}

export function reset()
{
    state.byMediaId = {}
    state.loaded = false
}
