import { createRouter, createWebHistory } from 'vue-router'
import { loadSession, currentUser } from '../stores/auth'

// Views behind a sign-in are split out, so the first paint of a public page
// does not carry the admin dashboard with it.
const routes = [
    {
        path: '/',
        name: 'home',
        component: () => import('../views/HomeView.vue'),
        meta: { title: 'Home' }
    },
    {
        path: '/browse',
        name: 'browse',
        component: () => import('../views/BrowseView.vue'),
        meta: { title: 'Browse' }
    },
    {
        path: '/media/:id',
        name: 'media',
        component: () => import('../views/MediaDetailsView.vue'),
        meta: { title: 'Title' }
    },
    {
        path: '/person/:id',
        name: 'person',
        component: () => import('../views/PersonView.vue'),
        meta: { title: 'Person' }
    },
    {
        path: '/login',
        name: 'login',
        component: () => import('../views/LoginView.vue'),
        meta: { title: 'Sign in', guestOnly: true }
    },
    {
        path: '/register',
        name: 'register',
        component: () => import('../views/RegisterView.vue'),
        meta: { title: 'Create account', guestOnly: true }
    },
    {
        path: '/library',
        name: 'library',
        component: () => import('../views/LibraryView.vue'),
        meta: { title: 'My Library', requiresAuth: true }
    },
    {
        path: '/profile',
        name: 'profile',
        component: () => import('../views/ProfileView.vue'),
        meta: { title: 'Profile', requiresAuth: true }
    },
    {
        path: '/admin',
        name: 'admin',
        component: () => import('../views/AdminView.vue'),
        meta: { title: 'Admin', requiresAuth: true, requiresAdmin: true }
    },
    {
        path: '/:pathMatch(.*)*',
        name: 'not-found',
        component: () => import('../views/NotFoundView.vue'),
        meta: { title: 'Not found' }
    }
]

const router = createRouter({
    history: createWebHistory(),
    routes,
    scrollBehavior(to, from, saved)
    {
        return saved ?? { top: 0 }
    }
})

router.beforeEach(async to =>
{
    // Resolved once and cached, so guarded navigations do not each cost a
    // round trip to /Auth/me.
    await loadSession()

    const user = currentUser.value

    if (to.meta.requiresAuth && !user)
    {
        return { path: '/login', query: { redirect: to.fullPath } }
    }

    if (to.meta.requiresAdmin && user?.role !== 'Admin')
    {
        return '/'
    }

    if (to.meta.guestOnly && user)
    {
        return '/'
    }

    return true
})

router.afterEach(to =>
{
    document.title = to.meta.title ? `${to.meta.title} · WatchSphere` : 'WatchSphere'
})

export default router
