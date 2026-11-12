// The ONE money formatter used on every page, so amounts look the same everywhere.
// See docs/adr/0001-money-representation.md. Display only: the UI never adds up money.
// IQD has no fractional digits in everyday use, so none are shown.

const formatters = new Map<string, Intl.NumberFormat>()

export function formatMoney(amount: number, currency = 'IQD', locale = 'en-IQ'): string {
  const key = `${locale}|${currency}`
  let formatter = formatters.get(key)
  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      maximumFractionDigits: currency === 'IQD' ? 0 : 2,
    })
    formatters.set(key, formatter)
  }
  return formatter.format(amount)
}
