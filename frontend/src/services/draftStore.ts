// IndexedDB stores images as Files; localStorage stores only non-secret form fields.
const DATABASE = 'ofertas-studio-drafts-v2'
const STORE = 'files'
export function openDraftDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(STORE)
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}
export async function saveDraftFiles(files: File[]): Promise<void> {
  const db = await openDraftDb()
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = db.transaction(STORE, 'readwrite')
      transaction.objectStore(STORE).put(files, 'images')
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error)
    })
  } finally { db.close() }
}
export async function loadDraftFiles(): Promise<File[]> {
  const db = await openDraftDb()
  try {
    return await new Promise<File[]>((resolve, reject) => {
      const request = db.transaction(STORE, 'readonly').objectStore(STORE).get('images')
      request.onsuccess = () => resolve((request.result as File[] | undefined) ?? [])
      request.onerror = () => reject(request.error)
    })
  } finally { db.close() }
}
export async function clearDraftFiles(): Promise<void> { await saveDraftFiles([]) }
