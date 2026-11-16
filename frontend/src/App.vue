<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { analyzeBatch, publishBatch, verifyApiKey } from './services/offerService'
import type { PublishOffer } from './services/offerService'
import { clearDraftFiles, loadDraftFiles, saveDraftFiles } from './services/draftStore'

type ItemStatus = 'pending' | 'analyzing' | 'ready' | 'analysis-error' | 'published' | 'failed' | 'uncertain'
type Item = PublishOffer & { id: string; preview: string; status: ItemStatus; issue: string }
const apiKey = ref('')
const showKey = ref(false)
const authBusy = ref(false)
const authError = ref('')
const authenticated = ref(false)
const rememberKey = ref(true)
const KEY_STORAGE = 'ofertas-studio-api-key-v1'
async function unlock() {
  const key = apiKey.value.trim()
  if (!key) { authError.value = 'Escribe tu API Key para continuar.'; return }
  authBusy.value = true
  authError.value = ''
  try {
    await verifyApiKey(key)
    apiKey.value = key
    if (rememberKey.value) localStorage.setItem(KEY_STORAGE, key)
    else localStorage.removeItem(KEY_STORAGE)
    authenticated.value = true
  } catch (e) {
    authenticated.value = false
    authError.value = e instanceof Error ? e.message : 'No pudimos comprobar tu clave.'
    // A rejected stored key must never auto-unlock again.
    localStorage.removeItem(KEY_STORAGE)
  } finally { authBusy.value = false }
}
function lockStudio() {
  if (busy.value) return
  authenticated.value = false
  apiKey.value = ''
  localStorage.removeItem(KEY_STORAGE)
  authError.value = ''
}

const items = ref<Item[]>([])
const activeId = ref('')
const busy = ref(false)
const modal = ref<'success' | 'error' | ''>('')
const confirmation = ref<'publish' | 'clear' | ''>('')
const publishCount = ref(0)
const confirmBusy = computed(() => busy.value)
function closeConfirmation() { if (!confirmBusy.value) confirmation.value = '' }
function acceptConfirmation() {
  if (confirmBusy.value) return
  const action = confirmation.value
  confirmation.value = ''
  if (action === 'clear') void clearAll()
  else if (action === 'publish') void publishConfirmed()
}
function onDialogKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && confirmation.value) { event.preventDefault(); closeConfirmation() }
}
const saved = ref(false)
const warning = ref('')
const fileInput = ref<HTMLInputElement | null>(null)
const ready = ref(false)
let saveTimer: ReturnType<typeof setTimeout> | undefined
let successTimer: ReturnType<typeof setTimeout> | undefined
let serial = 0
const STORAGE = 'ofertas-studio-batch-draft-v2'
const newItem = (file: File): Item => ({
  id: `${Date.now()}-${++serial}`, preview: URL.createObjectURL(file), status: 'pending', issue: '',
  image: file, imageUrl: '', title: '', store: 'MercadoLibre', price: 0, originalPrice: null,
  description: '', affiliateUrl: '', interestFreeMonths: null, monthlyPayment: null,
  interestFreeMonthsIsUpTo: false,
})
const selected = computed(() => items.value.find(x => x.id === activeId.value) ?? null)
const money = (n: number | null) => n == null ? '—' : new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(n)
const discount = computed(() => selected.value?.originalPrice && selected.value.price > 0 && selected.value.originalPrice > selected.value.price
  ? Math.round((1 - selected.value.price / selected.value.originalPrice) * 100) : null)
const validHttps = (url: string) => { try { return new URL(url).protocol === 'https:' } catch { return false } }
function validate(item: Item): string[] {
  const issues: string[] = []
  if (!item.image) issues.push('imagen')
  if (item.title.trim().length < 3 || item.title.length > 200) issues.push('título (3–200 caracteres)')
  if (!Number.isFinite(item.price) || item.price <= 0) issues.push('precio')
  if (item.originalPrice != null && (!Number.isFinite(item.originalPrice) || item.originalPrice <= item.price)) issues.push('precio anterior')
  if (!validHttps(item.affiliateUrl)) issues.push('enlace HTTPS')
  if (item.description.length > 400) issues.push('descripción (máximo 400)')
  if (item.interestFreeMonths != null && (!Number.isInteger(item.interestFreeMonths) || item.interestFreeMonths < 1 || item.interestFreeMonths > 60)) issues.push('MSI (1–60)')
  if (item.monthlyPayment != null && (item.interestFreeMonths == null || item.monthlyPayment <= 0)) issues.push('mensualidad')
  return issues
}
const pendingCount = computed(() => items.value.filter(x => x.status !== 'published').length)
const validCount = computed(() => items.value.filter(x => x.status !== 'published' && x.status !== 'uncertain' && validate(x).length === 0).length)
function pickFiles(files: FileList | File[] | null | undefined) {
  if (!files) return
  warning.value = ''
  const incoming = Array.from(files)
  if (incoming.length + items.value.length > 5) { warning.value = 'Máximo 5 imágenes por lote. Elimina alguna antes de agregar más.'; return }
  for (const file of incoming) {
    if (!['image/png', 'image/jpeg'].includes(file.type) || file.size > 10 * 1024 * 1024 || file.size === 0) {
      warning.value = `${file.name}: solo PNG/JPG de hasta 10 MB. WebP no se puede publicar en Telegram.`
      continue
    }
    const item = newItem(file)
    items.value.push(item)
    activeId.value = item.id
  }
  if (fileInput.value) fileInput.value.value = ''
  queueSave()
}
function removeItem(id: string) {
  const index = items.value.findIndex(x => x.id === id)
  if (index < 0) return
  URL.revokeObjectURL(items.value[index]!.preview)
  items.value.splice(index, 1)
  if (activeId.value === id) activeId.value = items.value[0]?.id ?? ''
  queueSave()
}
async function analyzeAll() {
  if (!apiKey.value.trim()) { warning.value = 'Introduce la API Key para analizar.'; return }
  const targets = items.value.filter(x => (x.status === 'pending' || x.status === 'analysis-error') && x.image)
  if (!targets.length) return
  busy.value = true; warning.value = ''
  targets.forEach(x => { x.status = 'analyzing'; x.issue = '' })
  try {
    const results = await analyzeBatch(targets.map(x => x.image!), apiKey.value.trim())
    for (const result of results) {
      const item = targets[result.index]
      if (!item) continue
      if (result.status !== 'analyzed' || !result.offer) {
        item.status = 'analysis-error'; item.issue = result.error ?? 'No se pudo leer la captura. Completa los datos manualmente.'
        continue
      }
      const data = result.offer
      item.title = data.title ?? ''
      item.store = data.store === 'Amazon' ? 'Amazon' : 'MercadoLibre'
      item.price = data.price ?? 0
      item.originalPrice = data.originalPrice ?? null
      item.description = data.description ?? ''
      item.interestFreeMonths = data.interestFreeMonths ?? null
      item.monthlyPayment = data.monthlyPayment ?? null
      item.interestFreeMonthsIsUpTo = data.interestFreeMonthsIsUpTo ?? false
      // Never overwrite an affiliate URL the user already pasted.
      item.status = 'ready'
      const missing = validate(item)
      item.issue = missing.length ? `Revisar: ${missing.join(', ')}` : ''
    }
  } catch (e) {
    targets.forEach(x => { x.status = 'analysis-error'; x.issue = e instanceof Error ? e.message : 'Análisis fallido' })
  } finally { busy.value = false; queueSave() }
}
function publishAll() {
  if (busy.value) return
  if (!apiKey.value.trim()) { warning.value = 'Introduce la API Key para publicar.'; return }
  const targets = items.value.filter(x => x.status !== 'published' && x.status !== 'uncertain')
  if (!targets.length) { warning.value = 'No hay ofertas pendientes confirmadas para publicar.'; return }
  targets.forEach(x => { x.issue = validate(x).join(', '); if (x.issue) x.status = 'failed' })
  const good = targets.filter(x => !x.issue)
  if (!good.length) { modal.value = 'error'; return }
  publishCount.value = good.length
  confirmation.value = 'publish'
}
async function publishConfirmed() {
  if (busy.value) return
  // Revalidate after confirmation: never publish stale or already published items.
  const good = items.value.filter(x => x.status !== 'published' && x.status !== 'uncertain' && validate(x).length === 0)
  if (!good.length) { warning.value = 'Ya no hay ofertas válidas para publicar.'; return }
  busy.value = true; warning.value = ''
  try {
    const results = await publishBatch(good, apiKey.value.trim())
    for (const result of results) {
      const item = good[result.index]
      if (!item) continue
      item.status = result.status === 'published' ? 'published' : result.status === 'uncertain' ? 'uncertain' : 'failed'
      item.issue = result.error ?? ''
    }
    modal.value = items.value.every(x => x.status === 'published') ? 'success' : 'error'
    if (modal.value === 'success') successTimer = setTimeout(() => { modal.value = ''; void clearAll() }, 2200)
  } catch (e) {
    good.forEach(x => { x.status = 'uncertain'; x.issue = 'Respuesta desconocida: revisa Telegram antes de reintentar.' })
    warning.value = e instanceof Error ? e.message : 'No se pudo confirmar el lote.'
    modal.value = 'error'
  } finally { busy.value = false; queueSave() }
}
async function clearAll() {
  if (saveTimer) clearTimeout(saveTimer)
  ready.value = false
  items.value.forEach(x => URL.revokeObjectURL(x.preview))
  items.value = []; activeId.value = ''; warning.value = ''; modal.value = ''; confirmation.value = ''; saved.value = false
  localStorage.removeItem(STORAGE)
  try { await clearDraftFiles() } catch { warning.value = 'No se pudo eliminar la copia local de imágenes.' }
  ready.value = true
}
function askClear() { if (!busy.value && items.value.length) confirmation.value = 'clear' }
function queueSave() {
  if (!ready.value) return
  saved.value = false
  if (saveTimer) clearTimeout(saveTimer)
  saveTimer = setTimeout(() => { void saveDraft() }, 250)
}
async function saveDraft() {
  if (!ready.value) return
  try {
    const snapshot = items.value.map(({ image: _image, preview: _preview, ...rest }) => rest)
    localStorage.setItem(STORAGE, JSON.stringify({ items: snapshot, activeId: activeId.value }))
    await saveDraftFiles(items.value.map(x => x.image!).filter(Boolean))
    saved.value = true
  } catch { saved.value = false; warning.value = 'No se pudo guardar el borrador. Revisa el almacenamiento del navegador.' }
}
watch(items, queueSave, { deep: true })
watch(activeId, queueSave)
onMounted(async () => {
  try {
    const storedKey = localStorage.getItem(KEY_STORAGE)
    if (storedKey) {
      apiKey.value = storedKey
      rememberKey.value = true
      await unlock()
    }
  } catch { authError.value = 'No fue posible recuperar la clave guardada.' }
  try {
    const raw = localStorage.getItem(STORAGE)
    if (raw) {
      const savedData = JSON.parse(raw) as { items: Omit<Item, 'image' | 'preview'>[]; activeId: string }
      const files = await loadDraftFiles()
      if (files.length === savedData.items.length) {
        items.value = savedData.items.map((x, i) => ({ ...x, image: files[i]!, preview: URL.createObjectURL(files[i]!) }))
        activeId.value = items.value.some(x => x.id === savedData.activeId) ? savedData.activeId : (items.value[0]?.id ?? '')
        saved.value = true
      } else warning.value = 'El navegador no pudo recuperar todas las imágenes. Selecciónalas nuevamente.'
    }
  } catch { warning.value = 'No se pudo restaurar el borrador.' }
  finally { ready.value = true }
})
onBeforeUnmount(() => {
  if (saveTimer) clearTimeout(saveTimer)
  if (successTimer) clearTimeout(successTimer)
  items.value.forEach(x => URL.revokeObjectURL(x.preview))
})
</script>

<template>
  <div v-if="!authenticated" class="auth-screen">
    <div class="auth-glow auth-glow-one"></div><div class="auth-glow auth-glow-two"></div>
    <div class="auth-panel">
      <div class="auth-logo-wrap"><img src="/logo-ofertas.jpg" alt="Logo de Ofertas Relámpago"/></div>
      <div class="auth-kicker">ACCESO PRIVADO · STUDIO</div>
      <h1>Tu centro de <em>ofertas.</em></h1>
      <p class="auth-intro">Conecta tu Studio una sola vez. Después podrás analizar capturas y publicar en Telegram sin volver a copiar la clave.</p>
      <form class="auth-form" @submit.prevent="unlock">
        <label for="studio-key">API Key de tu backend</label>
        <div class="auth-input-wrap"><input id="studio-key" v-model="apiKey" :type="showKey ? 'text' : 'password'" autocomplete="off" spellcheck="false" placeholder="Pega aquí tu X-API-Key" :disabled="authBusy" autofocus/><button type="button" class="auth-eye" :aria-label="showKey ? 'Ocultar clave' : 'Mostrar clave'" @click="showKey = !showKey">{{ showKey ? 'Ocultar' : 'Mostrar' }}</button></div>
        <label class="auth-remember"><input v-model="rememberKey" type="checkbox" :disabled="authBusy"/><span>Recordar en este dispositivo</span></label>
        <div v-if="authError" class="auth-error" role="alert">⚠ {{ authError }}</div>
        <button class="auth-submit" type="submit" :disabled="authBusy || !apiKey.trim()"><span v-if="authBusy" class="spinner"></span>{{ authBusy ? 'Verificando acceso…' : 'Entrar al Studio →' }}</button>
      </form>
      <p class="auth-footnote">Tu clave no se incluye en el código de la web. Si eliges «Recordar», se guarda en este navegador; úsalo solo en un dispositivo de confianza.</p>
    </div>
    <div class="auth-footer">OFERTAS RELÁMPAGO · TU PANEL PRIVADO ⚡</div>
  </div>
  <div v-else class="app-shell">
    <header class="topbar">
      <div class="brand"><img class="brand-logo" src="/logo-ofertas.jpg" alt="Ofertas Relámpago"/><div><strong>Ofertas Relámpago</strong><span>STUDIO / ADMIN</span></div></div>
      <div class="topbar-right"><button class="auth-logout" type="button" :disabled="busy" @click="lockStudio">Cambiar clave / Salir</button><span class="status-dot"></span><span>{{ saved ? 'Borrador guardado' : 'Guardando…' }}</span><span class="environment">LOTE V1</span></div>
    </header>
    <main class="workspace">
      <section class="page-heading"><div><div class="eyebrow">WORKSPACE / PUBLICACIÓN MASIVA</div><h1>Publica <em>hasta 5 ofertas.</em></h1><p>Analiza todas, corrige solo lo necesario y envía las válidas a Telegram.</p></div><div class="heading-badge">✦ Copilot SDK · Haiku 5.5</div></section>
      <div v-if="warning" class="notice notice-error" role="alert"><span>!</span><p>{{ warning }}</p><button @click="warning = ''">×</button></div>
      <div class="workspace-grid">
        <div class="editor-column">
          
          <section class="panel"><div class="section-title"><div class="step">01</div><div><h2>Importar capturas</h2><p>PNG o JPG · Hasta 10 MB cada una · Máximo 5</p></div><span class="section-tag">IA</span></div>
            <input ref="fileInput" class="visually-hidden" type="file" accept="image/png,image/jpeg" multiple @change="pickFiles(($event.target as HTMLInputElement).files)"/>
            <div class="dropzone" role="button" tabindex="0" @click="fileInput?.click()" @keydown.enter="fileInput?.click()" @keydown.space.prevent="fileInput?.click()" @dragover.prevent @drop.prevent="pickFiles($event.dataTransfer?.files)"><div class="upload-symbol">↥</div><strong>Selecciona hasta 5 capturas</strong><span>O arrástralas aquí · Puedes agregar imágenes después</span></div>
            <div class="batch-list"><button v-for="(item, i) in items" :key="item.id" class="batch-item" :class="{ active: activeId === item.id }" @click="activeId = item.id"><img :src="item.preview" alt="Captura"/><span><strong>#{{ i + 1 }} {{ item.title || item.image?.name }}</strong><small>{{ item.status === 'published' ? '✓ Publicada' : item.status === 'uncertain' ? '⚠ Confirmar en Telegram' : item.status === 'analysis-error' ? '⚠ Completar manualmente' : item.issue ? '⚠ Revisar campos' : item.status === 'ready' ? '✓ Analizada' : 'Pendiente' }}</small></span><span class="batch-remove" title="Eliminar" @click.stop="removeItem(item.id)">×</span></button></div>
            <button class="primary-button analyze-button" :disabled="busy || !items.some(x => x.status !== 'published')" @click="analyzeAll"><span v-if="busy" class="spinner"></span><span v-else>✦</span> Analizar capturas pendientes</button>
          </section>
          <section v-if="selected" class="panel"><div class="section-title"><div class="step">02</div><div><h2>Editar oferta {{ items.indexOf(selected) + 1 }} de {{ items.length }}</h2><p>Los campos faltantes se completan manualmente.</p></div><span class="section-tag">EDITABLE</span></div>
            <div v-if="selected.issue" class="notice notice-error">⚠ {{ selected.issue }}</div>
            <div class="form-fields">
              <label class="field full"><span>Nombre del producto *</span><input v-model="selected.title" maxlength="200" placeholder="Nombre del producto"/></label>
              <label class="field full"><span>Tienda *</span><select v-model="selected.store"><option value="MercadoLibre">Mercado Libre</option><option value="Amazon">Amazon</option></select></label>
              <label class="field"><span>Precio *</span><input v-model.number="selected.price" type="number" min="0.01" step="0.01"/></label>
              <label class="field"><span>Precio anterior</span><input v-model.number="selected.originalPrice" type="number" min="0.01" step="0.01"/></label>
              <div class="discount-line full"><span>Descuento</span><strong v-if="discount !== null">−{{ discount }}%</strong><span v-else>Sin descuento</span></div>
              <label class="field"><span>Meses sin intereses</span><input v-model.number="selected.interestFreeMonths" type="number" min="1" max="60" step="1" placeholder="Opcional"/></label>
              <label class="field"><span>Mensualidad</span><input v-model.number="selected.monthlyPayment" type="number" min="0.01" step="0.01" placeholder="Opcional"/></label>
              <label class="field full"><span><input v-model="selected.interestFreeMonthsIsUpTo" type="checkbox" style="width:auto;margin-right:8px"/>La promoción dice «Hasta X MSI»</span></label>
              <label class="field full"><span>Descripción ({{ selected.description.length }}/400)</span><textarea v-model="selected.description" maxlength="400" rows="3"/></label>
              <label class="field full"><span>Enlace de afiliado *</span><input v-model="selected.affiliateUrl" type="url" placeholder="https://meli.la/... o https://amzn.to/..."/></label>
              <div class="full validation"><span v-if="validate(selected).length">Falta corregir: {{ validate(selected).join(' · ') }}</span><span v-else>✓ Oferta lista para publicar</span></div>
            </div>
          </section>
          <section class="panel"><div class="section-title"><div class="step">03</div><div><h2>Publicación por lote</h2><p>Las ofertas con errores no bloquean a las demás.</p></div></div><button class="publish-button" :disabled="busy || validCount === 0" @click="publishAll"><span v-if="busy" class="spinner"></span><span v-else>➤</span> Publicar {{ validCount }} oferta(s) válidas</button><p class="batch-hint">{{ pendingCount }} pendientes · Las publicadas no se reenvían. Las inciertas requieren revisión manual.</p><button class="secondary-button" style="margin-top:12px;width:100%" :disabled="busy" @click="askClear">Limpiar formulario</button></section>
        </div>
        <aside class="preview-column"><section class="preview-panel"><div class="preview-heading"><div><div class="eyebrow">LIVE PREVIEW</div><h2>Vista previa Telegram</h2></div><span class="preview-pill">En vivo</span></div><div class="telegram-surface"><div class="telegram-header"><img class="telegram-avatar telegram-avatar-logo" src="/logo-ofertas.jpg" alt="Logo"/><div><strong>Ofertas Relámpago</strong><span>Vista aproximada</span></div></div><div class="telegram-message"><div class="message-image"><img v-if="selected" :src="selected.preview" alt="Captura"/><span v-else>Selecciona una captura</span></div><div v-if="selected" class="message-content"><div class="flash-title">⚡ OFERTA RELÁMPAGO</div><h3>{{ selected.title || 'Nombre del producto' }}</h3><div class="price-preview"><strong>{{ money(selected.price) }}</strong><span v-if="discount !== null" class="discount-badge">−{{ discount }}%</span></div><div v-if="selected.originalPrice" class="old-price">Antes: <s>{{ money(selected.originalPrice) }}</s></div><p v-if="selected.interestFreeMonths" class="preview-description">💳 {{ selected.interestFreeMonthsIsUpTo ? 'Hasta ' : '' }}{{ selected.interestFreeMonths }} MSI <span v-if="selected.monthlyPayment">· {{ money(selected.monthlyPayment) }} al mes</span></p><p class="preview-description">{{ selected.description }}</p><div class="preview-affiliate"><span>🛍️ Comprar en {{ selected.store === 'Amazon' ? 'Amazon' : 'Mercado Libre' }}:</span><a v-if="validHttps(selected.affiliateUrl)" :href="selected.affiliateUrl" target="_blank" rel="noopener noreferrer">{{ selected.affiliateUrl }}</a><span v-else>Agrega el enlace de afiliado</span></div><a class="preview-store-button" :class="selected.store === 'Amazon' ? 'preview-store-amazon' : 'preview-store-mercadolibre'" :href="validHttps(selected.affiliateUrl) ? selected.affiliateUrl : undefined" target="_blank" rel="noopener noreferrer" @click="!validHttps(selected.affiliateUrl) && $event.preventDefault()"><span class="store-brand">{{ selected.store === 'Amazon' ? 'amazon' : 'mercado libre' }}</span><span>Comprar en {{ selected.store === 'Amazon' ? 'Amazon' : 'Mercado Libre' }}</span> ↗</a></div></div></div><div class="preview-note">Telegram usa botones nativos. La captura original y el enlace visible se mantienen.</div></section></aside>
      </div>
      <footer class="footer"><span>ϟ Ofertas Relámpago Studio</span><span>Vue 3 + .NET 10 + Copilot SDK</span></footer>
    </main>
    <div v-if="confirmation" class="confirm-overlay" role="presentation" @click.self="closeConfirmation" @keydown="onDialogKeydown">
      <div class="confirm-card" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" aria-describedby="confirm-description" tabindex="-1">
        <div class="confirm-brand"><img src="/logo-ofertas.jpg" alt=""/><span>OFERTAS RELÁMPAGO · STUDIO</span></div>
        <div class="confirm-symbol" :class="confirmation">{{ confirmation === 'publish' ? '➤' : '⌫' }}</div>
        <h2 id="confirm-title">{{ confirmation === 'publish' ? `¿Publicar ${publishCount} oferta${publishCount === 1 ? '' : 's'}?` : '¿Descartar el borrador?' }}</h2>
        <p id="confirm-description">{{ confirmation === 'publish' ? 'Enviaremos a Telegram únicamente las ofertas válidas. Las que tengan errores permanecerán guardadas para corregirlas.' : 'Se eliminarán las capturas y los datos guardados en este dispositivo. Esta acción no se puede deshacer.' }}</p>
        <div class="confirm-actions">
          <button type="button" class="confirm-cancel" :disabled="confirmBusy" @click="closeConfirmation">{{ confirmation === 'publish' ? 'Seguir editando' : 'Conservar borrador' }}</button>
          <button type="button" class="confirm-accept" :class="confirmation" :disabled="confirmBusy" @click="acceptConfirmation">{{ confirmation === 'publish' ? 'Sí, publicar' : 'Sí, descartar' }}</button>
        </div>
      </div>
    </div>
    <div v-if="modal" class="result-overlay" role="dialog" aria-modal="true"><div class="result-card" :class="modal"><div class="result-icon">{{ modal === 'success' ? '✓' : '!' }}</div><h2>{{ modal === 'success' ? '¡Ofertas publicadas!' : 'Revisa las ofertas pendientes' }}</h2><p v-if="modal === 'success'">Telegram confirmó todas las publicaciones. El formulario se limpiará al terminar la animación.</p><p v-else>Las ofertas correctas ya se publicaron. Corrige los campos señalados; si alguna quedó incierta, verifica el canal antes de reenviarla.</p><div v-if="modal === 'error'" class="result-actions"><button class="primary-button" @click="modal = ''; publishAll()">Reintentar válidas</button><button class="secondary-button" @click="modal = ''; askClear()">Limpiar formulario</button><button class="secondary-button" @click="modal = ''">Seguir editando</button></div></div></div>
  </div>
</template>
