// Retry failed geocodes with simpler queries
const failed = [
  { name: "Farmers Branch, TX", query: "Farmers Branch, TX 75244" },
  { name: "Friendswood, TX", query: "Friendswood, TX 77546" },
  { name: "Katy, TX", query: "Katy, TX 77494" },
  { name: "Humble Outlet, TX", query: "Humble, TX 77338" },
  { name: "Perris Outlet, CA", query: "Perris, CA 92570" },
  { name: "Frisco, TX", query: "Frisco, TX 75035" },
  { name: "Gilbert, AZ", query: "Gilbert, AZ 85295" },
  { name: "Van Nuys, CA", query: "Van Nuys, CA 91402" },
]

async function geocode(q) {
  const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=1`
  const res = await fetch(url, { headers: { 'User-Agent': 'GatUs/1.0' } })
  const data = await res.json()
  return data.length > 0 ? { lat: parseFloat(data[0].lat), lng: parseFloat(data[0].lon) } : null
}

for (const f of failed) {
  const geo = await geocode(f.query)
  console.log(`${geo ? '✓' : '✗'} ${f.name}: ${geo ? `${geo.lat}, ${geo.lng}` : 'STILL NOT FOUND'}`)
  await new Promise(r => setTimeout(r, 1100))
}
