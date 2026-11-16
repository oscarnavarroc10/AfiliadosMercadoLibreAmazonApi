import type { AnalyzedOffer, StoreName } from '../types/offer'

async function ensureOk(response: Response): Promise<void> {
  if (response.ok) return
  const body = await response.text()
  let detail = body
  try {
    const data = JSON.parse(body) as { detail?: string; title?: string; message?: string; errors?: Record<string, string[]> }
    detail = data.detail ?? data.message ?? Object.values(data.errors ?? {}).flat().join(' · ') ?? data.title ?? body
    if (!detail) detail = data.title ?? body
  } catch { /* non-JSON error */ }
  throw new Error(`HTTP ${response.status}: ${detail.slice(0, 450)}`)
}

export async function analyzeOfferImage(image: File, apiKey: string): Promise<AnalyzedOffer> {
  const form = new FormData()
  form.append('image', image)
  const response = await fetch('/api/offers/analyze-image', {
    method: 'POST', headers: { 'X-API-Key': apiKey }, body: form,
  })
  await ensureOk(response)
  return await response.json() as AnalyzedOffer
}

export interface PublishOffer {
  title: string
  store: StoreName
  price: number
  originalPrice: number | null
  description: string
  affiliateUrl: string
  interestFreeMonths: number | null
  monthlyPayment: number | null
  interestFreeMonthsIsUpTo: boolean
  image: File | null
  imageUrl: string
}

export async function publishOffer(offer: PublishOffer, apiKey: string): Promise<void> {
  const form = new FormData()
  form.append('Title', offer.title)
  // Backend StoreName enum: Amazon = 0, MercadoLibre = 1.
  form.append('Store', offer.store === 'Amazon' ? '0' : '1')
  form.append('Price', String(offer.price))
  if (offer.originalPrice !== null) form.append('OriginalPrice', String(offer.originalPrice))
  form.append('AffiliateUrl', offer.affiliateUrl)
  if (offer.interestFreeMonths !== null) {
    form.append('InterestFreeMonths', String(offer.interestFreeMonths))
    if (offer.monthlyPayment !== null) form.append('MonthlyPayment', String(offer.monthlyPayment))
    form.append('InterestFreeMonthsIsUpTo', String(offer.interestFreeMonthsIsUpTo))
  }
  if (offer.description.trim()) form.append('Description', offer.description.trim())
  if (offer.image) form.append('Image', offer.image)
  else if (offer.imageUrl.trim()) form.append('ImageUrl', offer.imageUrl.trim())
  const response = await fetch('/api/telegram/publish', {
    method: 'POST', headers: { 'X-API-Key': apiKey }, body: form,
  })
  await ensureOk(response)
}

export interface BatchResult {
  index: number
  status: 'analyzed' | 'published' | 'failed' | 'uncertain'
  error?: string
  offer?: AnalyzedOffer
}

export async function analyzeBatch(images: File[], apiKey: string): Promise<BatchResult[]> {
  const form = new FormData()
  images.forEach((image) => form.append('images', image))
  const response = await fetch('/api/batch/analyze', { method: 'POST', headers: { 'X-API-Key': apiKey }, body: form })
  await ensureOk(response)
  return (await response.json() as { results: BatchResult[] }).results
}

export async function publishBatch(offers: PublishOffer[], apiKey: string): Promise<BatchResult[]> {
  const form = new FormData()
  offers.forEach((offer) => {
    if (!offer.image) throw new Error('Falta imagen en una oferta.')
    form.append('images', offer.image)
  })
  form.append('offersJson', JSON.stringify(offers.map(({ image: _image, imageUrl: _imageUrl, ...fields }) => fields)))
  const response = await fetch('/api/batch/publish', { method: 'POST', headers: { 'X-API-Key': apiKey }, body: form })
  await ensureOk(response)
  return (await response.json() as { results: BatchResult[] }).results
}
