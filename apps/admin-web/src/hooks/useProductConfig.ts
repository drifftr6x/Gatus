import { useQuery } from '@tanstack/react-query'
import { productApi } from '@/lib/api'

export function useProductConfig() {
  return useQuery({
    queryKey: ['product-config'],
    queryFn: productApi.get,
    staleTime: 5 * 60_000,
    retry: 2,
  })
}
