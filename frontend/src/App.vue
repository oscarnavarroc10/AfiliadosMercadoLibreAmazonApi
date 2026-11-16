<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { analyzeOfferImage, publishOffer } from './services/offerService'
import type { StoreName } from './types/offer'

const apiKey = ref('')
const showKey = ref(false)
const screenshot = ref<File | null>(null)
const screenshotPreview = ref('')
const title = ref('')
const store = ref<StoreName>('MercadoLibre')
const price = ref<number | null>(null)
const originalPrice = ref<number | null>(null)
const description = ref('')
const affiliateUrl = ref('')
const isAnalyzing = ref(false)
const isPublishing = ref(false)
const error = ref('')
const success = ref('')
const dragOver = ref(false)
const screenshotInput = ref<HTMLInputElement | null>(null)

const discount = computed(() => {
  if (!price.value || !originalPrice.value || originalPrice.value <= price.value) return null
  return Math.round((1 - price.value / originalPrice.value) * 100)
})
const money = (n: number | null) => n === null ? '—' : new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' }).format(n)
const previewImage = computed(() => screenshotPreview.value)
const hasImage = computed(() => Boolean(screenshot.value))
const canPublish = computed(() => Boolean(apiKey.value.trim() && title.value.trim().length >= 3 && price.value !== null && price.value > 0 && isHttpsUrl(affiliateUrl.value) && hasImage.value && !isPublishing.value && !isAnalyzing.value))

function isHttpsUrl(value: string): boolean {
  try { return new URL(value).protocol === 'https:' } catch { return false }
}
function clearMessages() { error.value = ''; success.value = '' }
function validFile(file: File): boolean {
  const allowed = ['image/png', 'image/jpeg', 'image/webp']
  if (!allowed.includes(file.type)) { error.value = 'Solo se permiten imágenes PNG, JPG o WebP.'; return false }
  if (file.size > 10 * 1024 * 1024) { error.value = 'La imagen no puede superar los 10 MB.'; return false }
  return true
}
function chooseScreenshot(file?: File) {
  clearMessages()
  if (!file || !validFile(file)) return
  screenshot.value = file
  // A new screenshot invalidates details extracted from the previous image.
  title.value = ''; price.value = null; originalPrice.value = null
  description.value = ''; affiliateUrl.value = ''
  if (screenshotPreview.value) URL.revokeObjectURL(screenshotPreview.value)
  screenshotPreview.value = URL.createObjectURL(file)
}
function onScreenshotChange(event: Event) {
  chooseScreenshot((event.target as HTMLInputElement).files?.[0])
}
function onDrop(event: DragEvent) {
  event.preventDefault(); dragOver.value = false
  chooseScreenshot(event.dataTransfer?.files[0])
}
async function analyze() {
  clearMessages()
  if (!apiKey.value.trim()) { error.value = 'Ingresa tu API Key local para analizar.'; return }
  if (!screenshot.value) { error.value = 'Primero selecciona una captura.'; return }
  isAnalyzing.value = true
  try {
    const analyzedFile = screenshot.value
    const result = await analyzeOfferImage(analyzedFile, apiKey.value.trim())
    if (screenshot.value !== analyzedFile) return
    title.value = result.title ?? ''
    store.value = result.store === 'Amazon' ? 'Amazon' : 'MercadoLibre'
    price.value = result.price
    originalPrice.value = result.originalPrice
    description.value = result.description ?? ''
    affiliateUrl.value = result.affiliateUrl ?? ''
    success.value = '¡Análisis completado! Revisa los datos y añade tu enlace de afiliado. La misma captura se enviará a Telegram.'
  } catch (e) { error.value = e instanceof Error ? e.message : 'No se pudo analizar la captura.' }
  finally { isAnalyzing.value = false }
}
async function publish() {
  clearMessages()
  if (!canPublish.value || price.value === null) { error.value = 'Completa título, precio, enlace HTTPS, captura y API Key.'; return }
  if (originalPrice.value !== null && originalPrice.value <= price.value) { error.value = 'El precio original debe ser mayor al precio actual.'; return }
  if (description.value.length > 400) { error.value = 'La descripción no debe superar 400 caracteres.'; return }
  if (!window.confirm('¿Publicar esta oferta en tu canal de Telegram?')) return
  isPublishing.value = true
  try {
    await publishOffer({ title: title.value.trim(), store: store.value, price: price.value, originalPrice: originalPrice.value, description: description.value, affiliateUrl: affiliateUrl.value.trim(), image: screenshot.value, imageUrl: '' }, apiKey.value.trim())
    success.value = '¡Oferta publicada correctamente en Telegram!'
  } catch (e) { error.value = e instanceof Error ? e.message : 'No se pudo publicar la oferta.' }
  finally { isPublishing.value = false }
}
onBeforeUnmount(() => {
  if (screenshotPreview.value) URL.revokeObjectURL(screenshotPreview.value)
})
</script>

<template>
  <div class="app-shell">
    <header class="topbar">
      <div class="brand">
        <img class="brand-logo" src="/logo-ofertas.jpg" alt="Logo de Ofertas Relámpago"/>
        <div><strong>Ofertas Relámpago</strong><span>STUDIO / ADMIN</span></div>
      </div>
      <div class="topbar-right"><span class="status-dot"></span><span>Panel de publicación</span><span class="environment">LOCAL DEV</span></div>
    </header>

    <main class="workspace">
      <section class="page-heading">
        <div><div class="eyebrow">WORKSPACE / NUEVA PUBLICACIÓN</div><h1>Publica una <em>gran oferta.</em></h1><p>De una captura a tu canal de Telegram. Sin copiar precios a mano.</p></div>
        <div class="heading-badge"><span class="pulse"></span> Copilot SDK · Haiku 5.5</div>
      </section>

      <div v-if="error" class="notice notice-error" role="alert"><span>!</span><p>{{ error }}</p><button aria-label="Cerrar error" @click="error = ''">×</button></div>
      <div v-if="success" class="notice notice-success" role="status"><span>✓</span><p>{{ success }}</p><button aria-label="Cerrar aviso" @click="success = ''">×</button></div>

      <div class="workspace-grid">
        <div class="editor-column">
          <section class="panel settings-panel">
            <div class="section-title"><div class="section-icon">⌘</div><div><h2>Conexión local</h2><p>Tu clave permanece en memoria mientras esta pestaña esté abierta.</p></div></div>
            <div class="key-row"><input v-model="apiKey" :type="showKey ? 'text' : 'password'" autocomplete="off" spellcheck="false" placeholder="X-API-Key · Introduce tu clave local" aria-label="API Key"/><button class="secondary-button key-toggle" @click="showKey = !showKey">{{ showKey ? 'Ocultar' : 'Mostrar' }}</button></div>
          </section>

          <section class="panel">
            <div class="section-title"><div class="step">01</div><div><h2>Importar captura</h2><p>La IA leerá título, tienda y precios directamente de la imagen.</p></div><span class="section-tag">IA</span></div>
            <input ref="screenshotInput" class="visually-hidden" type="file" accept="image/png,image/jpeg,image/webp" @change="onScreenshotChange" />
            <div class="dropzone" :class="{ 'drag-over': dragOver }" role="button" tabindex="0" @click="screenshotInput?.click()" @keydown.enter="screenshotInput?.click()" @keydown.space.prevent="screenshotInput?.click()" @dragover.prevent="dragOver = true" @dragleave.prevent="dragOver = false" @drop="onDrop">
              <template v-if="screenshotPreview"><img :src="screenshotPreview" alt="Captura seleccionada" class="screenshot-thumb"/><div class="dropzone-copy"><strong>{{ screenshot?.name }}</strong><span>Captura lista para analizar · Haz clic para cambiarla</span></div></template>
              <template v-else><div class="upload-symbol">↥</div><strong>Arrastra tu captura aquí</strong><span>o haz clic para seleccionar un archivo</span><small>PNG, JPG o WebP · Máximo 10 MB</small></template>
            </div>
            <button class="primary-button analyze-button" :disabled="isAnalyzing || isPublishing || !screenshot" @click="analyze"><span v-if="isAnalyzing" class="spinner"></span><span v-else>✦</span> {{ isAnalyzing ? 'Analizando captura...' : 'Analizar con inteligencia artificial' }} <span v-if="!isAnalyzing">→</span></button>
          </section>

          <section class="panel">
            <div class="section-title"><div class="step">02</div><div><h2>Detalles de la oferta</h2><p>La IA propone. Tú tienes la última palabra.</p></div><span class="section-tag">EDITABLE</span></div>
            <div class="form-fields">
              <label class="field full"><span>Nombre del producto <b>*</b></span><input v-model="title" maxlength="200" placeholder="Ej. Perfume Rasasi Hawas Kobra 100ml"/></label>
              <label class="field full"><span>Tienda <b>*</b></span><select v-model="store"><option value="MercadoLibre">Mercado Libre</option><option value="Amazon">Amazon</option></select></label>
              <label class="field"><span>Precio de oferta <b>*</b></span><div class="input-prefix"><span>$</span><input v-model.number="price" type="number" min="0.01" step="0.01" placeholder="593.01"/></div></label>
              <label class="field"><span>Precio anterior</span><div class="input-prefix"><span>$</span><input v-model.number="originalPrice" type="number" min="0.01" step="0.01" placeholder="1299.00"/></div></label>
              <div class="discount-line full"><span>Descuento calculado automáticamente</span><strong v-if="discount !== null">−{{ discount }}%</strong><span v-else class="muted">Sin descuento</span></div>
              <label class="field full"><span>Descripción <small>{{ description.length }}/400</small></span><textarea v-model="description" maxlength="400" rows="3" placeholder="Detalles destacados de la oferta..."></textarea></label>
              <label class="field full"><span>Enlace de afiliado <b>*</b></span><input v-model="affiliateUrl" type="url" placeholder="https://meli.la/... o https://amzn.to/..."/><small>Este enlace lo agregas tú. La IA nunca lo inventa.</small></label>
            </div>
          </section>

          <section class="panel">
            <div class="section-title"><div class="step">03</div><div><h2>Imagen para Telegram</h2><p>En esta V1 se publicará exactamente la misma captura que analizaste con IA.</p></div></div>
            <div class="image-source-v1">
              <img v-if="screenshotPreview" :src="screenshotPreview" alt="Imagen que se enviará a Telegram" />
              <div v-else class="image-source-empty">Selecciona una captura en el paso 01.</div>
              <p v-if="screenshot">✓ {{ screenshot.name }} · Archivo original, sin recortes ni modificaciones.</p>
            </div>
          </section>
        </div>

        <aside class="preview-column">
          <section class="preview-panel">
            <div class="preview-heading"><div><div class="eyebrow">LIVE PREVIEW</div><h2>Así se verá en Telegram</h2></div><span class="preview-pill"><span class="status-dot"></span> En vivo</span></div>
            <div class="telegram-surface">
              <div class="telegram-header"><img class="telegram-avatar telegram-avatar-logo" src="/logo-ofertas.jpg" alt="Logo del canal"/><div><strong>Ofertas Relámpago</strong><span>Vista previa aproximada</span></div><span class="telegram-menu">•••</span></div>
              <div class="telegram-message"><div class="message-image" :class="{ empty: !previewImage }"><img v-if="previewImage" :src="previewImage" alt="Captura que se publicará en Telegram" @error="() => {}"/><template v-else><div class="image-placeholder-icon">▧</div><span>La captura de tu oferta aparecerá aquí</span></template></div><div class="message-content"><div class="flash-title">⚡ OFERTA RELÁMPAGO</div><h3>{{ title || 'El nombre de tu producto aparecerá aquí' }}</h3><div class="price-preview"><strong>{{ money(price) }}</strong><span v-if="discount !== null" class="discount-badge">−{{ discount }}%</span></div><div v-if="originalPrice !== null" class="old-price">Antes: <s>{{ money(originalPrice) }}</s></div><p v-if="description" class="preview-description">{{ description }}</p><div class="store-label">{{ store === 'Amazon' ? '🟠 Amazon' : '🟡 Mercado Libre' }}</div><div class="preview-link">Comprar en {{ store === 'Amazon' ? 'Amazon' : 'Mercado Libre' }} ↗</div></div></div>
            </div>
            <div class="preview-note">ⓘ La publicación final la genera tu <code>OfferCaptionBuilder</code> del backend. Esta vista es una aproximación visual.</div>
            <div class="publish-area"><button class="publish-button" :disabled="!canPublish" @click="publish"><span v-if="isPublishing" class="spinner"></span><span v-else>➤</span> {{ isPublishing ? 'Publicando...' : 'Publicar en Telegram' }}</button><p>Confirma antes de enviar · No se publica automáticamente</p></div>
          </section>
          <div class="side-tip"><span>✦</span><div><strong>Tu flujo, simplificado</strong><p>1. Captura → 2. Analizar → 3. Revisar → 4. Añadir enlace → 5. Publicar.</p></div></div>
        </aside>
      </div>
      <footer class="footer"><span>ϟ Ofertas Relámpago Studio</span><span>Vue 3 + .NET 10 + GitHub Copilot SDK</span></footer>
    </main>
  </div>
</template>
