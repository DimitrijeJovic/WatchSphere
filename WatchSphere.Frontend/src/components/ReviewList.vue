<template>
  <section class="reviews">
    <div class="section-head">
      <div>
        <span class="eyebrow">Community</span>
        <h2>Reviews</h2>
      </div>

      <div class="head-actions">
        <select v-model="sortBy" class="sort" aria-label="Sort reviews" @change="fetch">
          <option value="helpful">Most helpful</option>
          <option value="recent">Most recent</option>
        </select>

        <button v-if="loggedIn && !myReview && !composing" class="btn btn-sm" type="button" @click="startWrite">
          Write a review
        </button>
      </div>
    </div>

    <form v-if="composing" class="composer panel" @submit.prevent="submit">
      <div class="composer-head">
        <h3>{{ editingId ? 'Edit your review' : 'Write a review' }}</h3>

        <button class="btn btn-quiet btn-sm" type="button" @click="cancel">Cancel</button>
      </div>

      <p v-if="formError" class="notice notice-error">{{ formError }}</p>

      <div class="score-picker">
        <span class="eyebrow">Score</span>

        <div class="score-row">
          <button
            v-for="n in 10"
            :key="n"
            type="button"
            class="score-chip"
            :class="{ on: form.rating >= n, picked: form.rating === n }"
            :aria-pressed="form.rating === n"
            @click="form.rating = n"
          >{{ n }}</button>
        </div>
      </div>

      <textarea
        v-model="form.reviewText"
        placeholder="What did you make of it?"
        maxlength="4000"
        required
      ></textarea>

      <div class="composer-foot">
        <label class="spoiler">
          <input v-model="form.containsSpoilers" type="checkbox" />
          <span>This review gives away plot details</span>
        </label>

        <span class="counter num">{{ form.reviewText.length }}/4000</span>

        <button class="btn btn-sm" type="submit" :disabled="saving || form.rating === 0">
          {{ editingId ? 'Save changes' : 'Publish review' }}
        </button>
      </div>
    </form>

    <p v-if="loading" class="muted">Loading reviews…</p>

    <div v-else-if="reviews.length === 0" class="empty">
      <strong>No reviews yet.</strong>
      Be the first to say what you thought of it.
    </div>

    <article v-for="review in reviews" v-else :key="review.id" class="review panel">
      <header class="review-head">
        <span v-if="review.avatarUrl" class="avatar"><img :src="review.avatarUrl" alt="" /></span>
        <span v-else class="avatar initial">{{ review.username.charAt(0).toUpperCase() }}</span>

        <div class="who">
          <span class="name">{{ review.username }}</span>
          <span class="when num">{{ formatDate(review.createdAt) }}<template v-if="review.updatedAt"> · edited</template></span>
        </div>

        <span class="verdict num">
          {{ review.rating }}<span class="of">/10</span>
        </span>
      </header>

      <div v-if="review.containsSpoilers && !revealed.has(review.id)" class="spoiler-wall">
        <p>This review discusses plot details.</p>

        <button class="btn btn-quiet btn-sm" type="button" @click="revealed.add(review.id)">
          Show anyway
        </button>
      </div>

      <p v-else class="body">{{ review.reviewText }}</p>

      <footer class="review-foot">
        <button
          v-if="loggedIn && !review.isMine"
          class="vote"
          :class="{ on: review.myVote === true }"
          type="button"
          @click="vote(review, true)"
        >
          Helpful <span class="num">{{ review.helpfulCount }}</span>
        </button>

        <span v-else class="vote static">
          Helpful <span class="num">{{ review.helpfulCount }}</span>
        </span>

        <button class="link" type="button" @click="toggleComments(review)">
          {{ openComments.has(review.id) ? 'Hide' : 'Comments' }}
          <span class="num">{{ review.commentsCount }}</span>
        </button>

        <template v-if="review.isMine">
          <button class="link" type="button" @click="startEdit(review)">Edit</button>
          <button class="link danger" type="button" @click="remove(review)">Delete</button>
        </template>

        <button v-else-if="isAdmin" class="link danger" type="button" @click="remove(review)">
          Remove
        </button>
      </footer>

      <div v-if="openComments.has(review.id)" class="thread">
        <p v-if="!comments[review.id]" class="muted small">Loading…</p>

        <template v-else>
          <div v-for="comment in comments[review.id]" :key="comment.id" class="comment">
            <span class="comment-name">{{ comment.username }}</span>
            <span class="comment-when num">{{ formatDate(comment.createdAt) }}</span>
            <p class="comment-body">{{ comment.text }}</p>

            <button
              v-if="comment.isMine || isAdmin"
              class="link danger tiny"
              type="button"
              @click="removeComment(review, comment)"
            >Delete</button>
          </div>

          <p v-if="comments[review.id].length === 0" class="muted small">No comments yet.</p>

          <form v-if="loggedIn" class="comment-form" @submit.prevent="addComment(review)">
            <input
              v-model="commentDrafts[review.id]"
              type="text"
              maxlength="1000"
              placeholder="Add a comment"
            />
            <button class="btn btn-sm" type="submit" :disabled="!commentDrafts[review.id]?.trim()">
              Post
            </button>
          </form>
        </template>
      </div>
    </article>
  </section>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import client, { errorMessage } from '../api/client'
import { isLoggedIn, isAdmin } from '../stores/auth'

const props = defineProps({
  mediaId: { type: Number, required: true }
})

const emit = defineEmits(['changed'])

const loggedIn = isLoggedIn

const loading = ref(true)
const saving = ref(false)
const reviews = ref([])
const sortBy = ref('helpful')
const composing = ref(false)
const editingId = ref(null)
const formError = ref('')

const revealed = reactive(new Set())
const openComments = reactive(new Set())
const comments = reactive({})
const commentDrafts = reactive({})

const form = reactive({
  rating: 0,
  reviewText: '',
  containsSpoilers: false
})

const myReview = computed(() => reviews.value.find(review => review.isMine) ?? null)

function formatDate(value)
{
  return new Date(value).toLocaleDateString('en-GB', {
    day: 'numeric', month: 'short', year: 'numeric'
  })
}

async function fetch()
{
  loading.value = true

  try
  {
    const response = await client.get(`/Review/media/${props.mediaId}`, {
      params: { sortBy: sortBy.value }
    })

    reviews.value = response.data
  }
  finally
  {
    loading.value = false
  }
}

function startWrite()
{
  editingId.value = null
  form.rating = 0
  form.reviewText = ''
  form.containsSpoilers = false
  formError.value = ''
  composing.value = true
}

function startEdit(review)
{
  editingId.value = review.id
  form.rating = review.rating
  form.reviewText = review.reviewText
  form.containsSpoilers = review.containsSpoilers
  formError.value = ''
  composing.value = true
}

function cancel()
{
  composing.value = false
  formError.value = ''
}

async function submit()
{
  saving.value = true
  formError.value = ''

  const body = {
    mediaId: props.mediaId,
    rating: form.rating,
    reviewText: form.reviewText,
    containsSpoilers: form.containsSpoilers
  }

  try
  {
    if (editingId.value)
    {
      await client.put(`/Review/${editingId.value}`, body)
    }
    else
    {
      await client.post('/Review', body)
    }

    composing.value = false

    await fetch()

    // The title's average moved, so the page above needs to reload its header.
    emit('changed')
  }
  catch (e)
  {
    formError.value = errorMessage(e, 'Could not save your review.')
  }
  finally
  {
    saving.value = false
  }
}

async function remove(review)
{
  if (!window.confirm('Delete this review? This cannot be undone.'))
  {
    return
  }

  await client.delete(`/Review/${review.id}`)

  await fetch()

  emit('changed')
}

async function vote(review, helpful)
{
  const response = await client.post(`/Review/${review.id}/vote`, null, {
    params: { helpful }
  })

  Object.assign(review, response.data)
}

async function toggleComments(review)
{
  if (openComments.has(review.id))
  {
    openComments.delete(review.id)

    return
  }

  openComments.add(review.id)

  if (!comments[review.id])
  {
    const response = await client.get(`/Review/${review.id}/comments`)

    comments[review.id] = response.data
  }
}

async function addComment(review)
{
  const text = commentDrafts[review.id]?.trim()

  if (!text)
  {
    return
  }

  const response = await client.post(`/Review/${review.id}/comments`, { text })

  comments[review.id].push(response.data)
  commentDrafts[review.id] = ''
  review.commentsCount += 1
}

async function removeComment(review, comment)
{
  await client.delete(`/Review/comments/${comment.id}`)

  comments[review.id] = comments[review.id].filter(c => c.id !== comment.id)
  review.commentsCount -= 1
}

onMounted(fetch)
</script>

<style scoped>
.reviews { display: grid; gap: 0.9rem; }

.head-actions { display: flex; align-items: center; gap: 0.6rem; }

.sort { width: auto; padding: 0.42rem 0.6rem; font-size: 0.8rem; }

.muted { color: var(--ws-dim); font-size: 0.86rem; }

.small { font-size: 0.8rem; }

/* --- composer ------------------------------------------------------------- */

.composer { padding: 1.1rem 1.2rem; display: grid; gap: 0.85rem; }

.composer-head { display: flex; align-items: center; justify-content: space-between; }

.score-picker { display: grid; gap: 0.4rem; }

/* Ten numbered chips rather than a star widget: the API stores a whole number
   out of ten, and the control should say so. */
.score-row { display: flex; flex-wrap: wrap; gap: 4px; }

.score-chip {
  width: 34px;
  height: 32px;
  border: 1px solid var(--ws-line-strong);
  border-radius: 7px;
  background: transparent;
  color: var(--ws-faint);
  font-family: 'IBM Plex Mono', monospace;
  font-size: 0.8rem;
  font-weight: 600;
  transition: background 0.14s var(--ws-ease), color 0.14s var(--ws-ease), border-color 0.14s var(--ws-ease);
}

.score-chip.on { background: rgba(225, 29, 46, 0.16); border-color: rgba(225, 29, 46, 0.4); color: #ff9aa4; }

.score-chip.picked { background: var(--ws-red); border-color: var(--ws-red); color: #fff; }

.composer-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 0.9rem; }

.spoiler { display: flex; align-items: center; gap: 0.45rem; font-size: 0.82rem; color: var(--ws-dim); }

.spoiler input { width: auto; }

.counter { margin-left: auto; font-size: 0.72rem; color: var(--ws-faint); }

/* --- review --------------------------------------------------------------- */

.review { padding: 1.05rem 1.15rem; display: grid; gap: 0.7rem; }

.review-head { display: flex; align-items: center; gap: 0.65rem; }

.avatar {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  overflow: hidden;
  flex-shrink: 0;
  background: var(--ws-card);
}

.avatar img { width: 100%; height: 100%; object-fit: cover; }

.initial {
  display: grid;
  place-items: center;
  background: var(--ws-card-hi);
  color: var(--ws-dim);
  font-size: 0.85rem;
  font-weight: 700;
}

.who { display: flex; flex-direction: column; line-height: 1.3; }

.name { font-size: 0.88rem; font-weight: 600; }

.when { font-size: 0.71rem; color: var(--ws-faint); }

.verdict { margin-left: auto; font-size: 1.05rem; font-weight: 600; color: var(--ws-amber); }

.of { font-size: 0.72rem; color: var(--ws-faint); }

.body { font-size: 0.9rem; line-height: 1.65; color: #cfcfd7; white-space: pre-wrap; }

.spoiler-wall {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.8rem;
  padding: 0.9rem 1rem;
  border: 1px dashed var(--ws-line-strong);
  border-radius: var(--ws-radius-sm);
  font-size: 0.84rem;
  color: var(--ws-dim);
}

.review-foot { display: flex; flex-wrap: wrap; align-items: center; gap: 1rem; }

.vote {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.3rem 0.6rem;
  border: 1px solid var(--ws-line-strong);
  border-radius: 999px;
  background: transparent;
  color: var(--ws-dim);
  font-size: 0.76rem;
  font-weight: 600;
  transition: color 0.15s var(--ws-ease), border-color 0.15s var(--ws-ease);
}

.vote:hover { color: var(--ws-text); }

.vote.on { color: var(--ws-green); border-color: rgba(62, 207, 142, 0.4); }

.vote.static { cursor: default; }

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--ws-faint);
  font-size: 0.76rem;
  font-weight: 600;
  transition: color 0.15s var(--ws-ease);
}

.link:hover { color: var(--ws-text); }

.link.danger:hover { color: #ff6070; }

.link.tiny { font-size: 0.7rem; }

/* --- comments ------------------------------------------------------------- */

.thread {
  display: grid;
  gap: 0.7rem;
  padding-top: 0.8rem;
  border-top: 1px solid var(--ws-line);
}

.comment { display: grid; grid-template-columns: auto auto 1fr auto; gap: 0.5rem; align-items: baseline; }

.comment-name { font-size: 0.8rem; font-weight: 600; }

.comment-when { font-size: 0.68rem; color: var(--ws-faint); }

.comment-body { grid-column: 1 / -1; font-size: 0.85rem; line-height: 1.55; color: #c3c3cc; }

.comment-form { display: flex; gap: 0.5rem; }

.comment-form input { font-size: 0.85rem; }
</style>
