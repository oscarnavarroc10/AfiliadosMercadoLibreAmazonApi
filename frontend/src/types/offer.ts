export type StoreName = 'Amazon' | 'MercadoLibre'
export interface AnalyzedOffer {
  title: string | null
  store: StoreName | null
  price: number | null
  originalPrice: number | null
  currency: string | null
  description: string | null
  affiliateUrl: string | null
  interestFreeMonths: number | null
  monthlyPayment: number | null
  interestFreeMonthsIsUpTo: boolean
}
