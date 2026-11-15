import axios from 'axios'

/**
 * One configured axios instance for the whole app. Components used to build
 * their own URLs and attach the Authorization header by hand, which meant the
 * API host was hard-coded in a dozen places.
 */
const client = axios.create({
    baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5214/api'
})

export const TOKEN_KEY = 'watchsphere.token'

client.interceptors.request.use(config =>
{
    const token = localStorage.getItem(TOKEN_KEY)

    if (token)
    {
        config.headers.Authorization = `Bearer ${token}`
    }

    return config
})

client.interceptors.response.use(
    response => response,
    error =>
    {
        // An expired or revoked token should drop the session rather than leave
        // the UI in a half-signed-in state.
        if (error.response?.status === 401)
        {
            window.dispatchEvent(new Event('watchsphere:unauthorized'))
        }

        return Promise.reject(error)
    }
)

/** Pulls the readable message out of a ProblemDetails or plain error body. */
export function errorMessage(error, fallback = 'Something went wrong.')
{
    return error.response?.data?.message
        ?? error.response?.data?.detail
        ?? error.response?.data?.title
        ?? fallback
}

export default client
