// Geocode Living Spaces store addresses using Nominatim (OpenStreetMap)
// Run: node scripts/geocode-stores.mjs

const stores = [
  { name: "Farmers Branch, TX", address: "13307 Midway Road, Suite 100, Farmers Branch, TX 75244" },
  { name: "Lone Tree, CO", address: "10668 Cabela Dr, Lone Tree, CO 80124" },
  { name: "Fresno, CA", address: "7354 N. Abby St, Fresno, CA 93720" },
  { name: "Friendswood, TX", address: "300 Baybrook Mall, Friendswood, TX 77546" },
  { name: "Conroe, TX", address: "10900 Interstate 45 S, Conroe, TX 77304" },
  { name: "Thornton, CO", address: "16395 Washington St, Thornton, CO 80023" },
  { name: "Central Houston, TX", address: "2660 Fountain View Dr, Houston, TX 77057" },
  { name: "Clearfield, UT", address: "1400 E 700 South, Clearfield, UT 84015" },
  { name: "Lenexa, KS", address: "12381 W 95th St, Lenexa, KS 66215" },
  { name: "Oklahoma City, OK", address: "13502 N May Ave, Oklahoma City, OK 73120" },
  { name: "Draper, UT", address: "13004 S Pony Express Rd, Draper, UT 84020" },
  { name: "Katy, TX", address: "444 Katy Village Pkwy, Katy, TX 77494" },
  { name: "Humble Outlet, TX", address: "18240 Hwy 59, Humble, TX 77338" },
  { name: "Fort Worth, TX", address: "8640 Tehama Ridge Pkwy, Fort Worth, TX 76177" },
  { name: "Manteca, CA", address: "1355 W Atherton Dr, Manteca, CA 95337" },
  { name: "Cibolo, TX", address: "17782 I-35 N, Schertz, TX 78154" },
  { name: "Perris Outlet, CA", address: "18810 Harvill Ave, Perris, CA 92570" },
  { name: "Roseville, CA", address: "1851 Freedom Way, Roseville, CA 95678" },
  { name: "Frisco, TX", address: "10900 St Hwy 121, Frisco, TX 75035" },
  { name: "San Jose, CA", address: "5540 Winfield Blvd, San Jose, CA 95123" },
  { name: "Grand Prairie, TX", address: "1514 Arkansas Ln, Grand Prairie, TX 75052" },
  { name: "San Antonio, TX", address: "4239 N Loop 1604 W, San Antonio, TX 78249" },
  { name: "Fremont, CA", address: "49088 Fremont Blvd, Fremont, CA 94537" },
  { name: "Gilbert, AZ", address: "2300 S San Tan Village Pkwy, Gilbert, AZ 85295" },
  { name: "Glendale, AZ", address: "6767 W Bell Rd, Glendale, AZ 85308" },
  { name: "Huntington Beach, CA", address: "6912 Edinger Ave, Huntington Beach, CA 92647" },
  { name: "Irvine, CA", address: "101 Technology Dr, Irvine, CA 92618" },
  { name: "La Mirada, CA", address: "14501 Artesia Blvd, La Mirada, CA 90638" },
  { name: "Menifee, CA", address: "30251 Antelope Rd, Menifee, CA 92584" },
  { name: "Mid City Los Angeles, CA", address: "4801 Venice Blvd, Los Angeles, CA 90019" },
  { name: "Millbrae, CA", address: "855 Broadway, Millbrae, CA 94030" },
  { name: "Mission Valley, CA", address: "8730 Rio San Diego, San Diego, CA 92108" },
  { name: "Monrovia, CA", address: "407 W Huntington Dr, Monrovia, CA 91016" },
  { name: "Phoenix, AZ", address: "6600 W Latham, Phoenix, AZ 85043" },
  { name: "Rancho Cucamonga, CA", address: "12649 Foothill Blvd, Rancho Cucamonga, CA 91739" },
  { name: "Redondo Beach, CA", address: "1519 Hawthorne Blvd, Redondo Beach, CA 90278" },
  { name: "San Leandro, CA", address: "250 Floresta Blvd, San Leandro, CA 94578" },
  { name: "Scottsdale, AZ", address: "16275 N Scottsdale Rd, Scottsdale, AZ 85260" },
  { name: "Summerlin, NV", address: "700 S Rampart Blvd, Las Vegas, NV 89145" },
  { name: "Van Nuys, CA", address: "14400 Arminta St, Van Nuys, CA 91402" },
  { name: "Vista, CA", address: "1900 University Dr, Vista, CA 92083" },
  { name: "Fremont DC, CA", address: "41088 Boyce Rd, Fremont, CA 94538" },
  { name: "Rialto DC, CA", address: "3994 S Riverside Ave, Colton, CA 92324" },
  { name: "Pflugerville, TX", address: "19024 N Heatherwilde, Pflugerville, TX 78660" },
  { name: "Buford, GA", address: "2630 Gravel Springs Rd, Buford, GA 30519" },
]

async function geocode(address) {
  const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(address)}&limit=1`
  const res = await fetch(url, {
    headers: { 'User-Agent': 'GatUs-Kiosk-Admin/1.0' }
  })
  const data = await res.json()
  if (data.length > 0) {
    return { lat: parseFloat(data[0].lat), lng: parseFloat(data[0].lon) }
  }
  return null
}

const results = []
for (const store of stores) {
  try {
    const geo = await geocode(store.address)
    results.push({
      ...store,
      lat: geo?.lat ?? null,
      lng: geo?.lng ?? null,
    })
    console.log(`${geo ? '✓' : '✗'} ${store.name}: ${geo ? `${geo.lat}, ${geo.lng}` : 'NOT FOUND'}`)
    // Nominatim rate limit: 1 request per second
    await new Promise(r => setTimeout(r, 1100))
  } catch (err) {
    console.error(`✗ ${store.name}: ${err.message}`)
    results.push({ ...store, lat: null, lng: null })
  }
}

// Output as SQL
console.log('\n--- SQL ---')
results.forEach(r => {
  if (r.lat && r.lng) {
    console.log(`-- ${r.name}`)
    console.log(`-- UPDATE devices SET latitude = ${r.lat}, longitude = ${r.lng} WHERE location LIKE '%${r.name.split(',')[0]}%';`)
  }
})

console.log('\n--- JSON ---')
console.log(JSON.stringify(results, null, 2))
