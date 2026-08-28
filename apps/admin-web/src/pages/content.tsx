import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { contentApi } from '@/lib/api'
import type { ContentDto } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2, Image, Video, FileText, Globe, FileCode } from 'lucide-react'

const typeIcons: Record<string, typeof Image> = {
  Image: Image,
  Video: Video,
  Html: FileCode,
  Pdf: FileText,
  Url: Globe,
}

export function ContentPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingContent, setEditingContent] = useState<ContentDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['content'],
    queryFn: () => contentApi.list(),
  })

  const deleteMutation = useMutation({
    mutationFn: contentApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['content'] })
    },
  })

  const handleDelete = (id: string) => {
    if (confirm('Are you sure you want to delete this content?')) {
      deleteMutation.mutate(id)
    }
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Content</h1>
          <p className="mt-1 text-sm text-slate-400">Media and pages deployed to kiosks</p>
        </div>
        <button
          onClick={() => {
            setEditingContent(null)
            setIsModalOpen(true)
          }}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Add Content
        </button>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error loading content: {error.message}
        </div>
      ) : (
        <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
          {data?.contents.map((content) => {
            const TypeIcon = typeIcons[content.type] ?? FileText
            return (
              <div
                key={content.id}
                className="overflow-hidden rounded-xl border border-surface-800 bg-surface-900 shadow-lg transition-colors hover:border-surface-700"
              >
                <div className="flex h-36 items-center justify-center bg-surface-850">
                  {content.thumbnailUrl ? (
                    <img
                      src={content.thumbnailUrl}
                      alt={content.name}
                      className="h-full w-full object-cover"
                    />
                  ) : (
                    <TypeIcon className="h-10 w-10 text-slate-600" />
                  )}
                </div>
                <div className="p-4">
                  <h3 className="font-medium text-slate-100">{content.name}</h3>
                  <p className="mt-1 line-clamp-2 text-sm text-slate-400">
                    {content.description || 'No description'}
                  </p>
                  <div className="mt-4 flex items-center justify-between">
                    <span className="rounded-md bg-surface-800 px-2 py-0.5 text-xs text-slate-400">
                      {content.type} • {formatFileSize(content.fileSizeBytes)}
                    </span>
                    <div className="flex gap-1">
                      <button
                        onClick={() => {
                          setEditingContent(content)
                          setIsModalOpen(true)
                        }}
                        className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleDelete(content.id)}
                        className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      )}

      {!isLoading && !error && data?.contents.length === 0 && (
        <div className="py-12 text-center text-sm text-slate-500">
          No content found. Add your first content to get started.
        </div>
      )}

      {isModalOpen && (
        <ContentModal content={editingContent} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes'
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const inputClass =
  'mt-1.5 block w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white placeholder-slate-500 outline-none transition-colors focus:border-accent-500 focus:ring-1 focus:ring-accent-500'

function ContentModal({
  content,
  onClose,
}: {
  content: ContentDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [formData, setFormData] = useState({
    name: content?.name || '',
    description: content?.description || '',
    type: content?.type || 'Image',
    url: content?.url || '',
    thumbnailUrl: content?.thumbnailUrl || '',
  })

  const mutation = useMutation({
    mutationFn: (data: typeof formData) =>
      content ? contentApi.update(content.id, data) : contentApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['content'] })
      onClose()
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    mutation.mutate(formData)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {content ? 'Edit Content' : 'Add Content'}
        </h2>
        <form onSubmit={handleSubmit} className="mt-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className={inputClass}
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input
              type="text"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              className={inputClass}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Type</label>
            <select
              value={formData.type}
              onChange={(e) => setFormData({ ...formData, type: e.target.value })}
              className={inputClass}
            >
              <option value="Image">Image</option>
              <option value="Video">Video</option>
              <option value="Html">HTML</option>
              <option value="Pdf">PDF</option>
              <option value="Url">URL</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">URL</label>
            <input
              type="url"
              value={formData.url}
              onChange={(e) => setFormData({ ...formData, url: e.target.value })}
              className={inputClass}
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Thumbnail URL</label>
            <input
              type="url"
              value={formData.thumbnailUrl}
              onChange={(e) => setFormData({ ...formData, thumbnailUrl: e.target.value })}
              className={inputClass}
            />
          </div>
          <div className="mt-6 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 transition-colors hover:bg-surface-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400 disabled:opacity-50"
            >
              {mutation.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
