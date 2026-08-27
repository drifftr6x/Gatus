import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { contentApi } from '@/lib/api'
import type { ContentDto } from '@/lib/api'
import { useState } from 'react'

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

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-slate-900" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
        Error loading content: {error.message}
      </div>
    )
  }

  return (
    <div>
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-semibold text-slate-900">Content</h1>
        <button
          onClick={() => {
            setEditingContent(null)
            setIsModalOpen(true)
          }}
          className="px-4 py-2 bg-slate-900 text-white rounded-md hover:bg-slate-800"
        >
          Add Content
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {data?.contents.map((content) => (
          <div key={content.id} className="bg-white shadow rounded-lg overflow-hidden">
            <div className="h-48 bg-slate-200 flex items-center justify-center">
              {content.thumbnailUrl ? (
                <img
                  src={content.thumbnailUrl}
                  alt={content.name}
                  className="h-full w-full object-cover"
                />
              ) : (
                <span className="text-slate-400 text-4xl">{content.type}</span>
              )}
            </div>
            <div className="p-4">
              <h3 className="text-lg font-medium text-slate-900">{content.name}</h3>
              <p className="text-sm text-slate-500 mt-1">{content.description || 'No description'}</p>
              <div className="mt-4 flex justify-between items-center">
                <span className="text-xs text-slate-400">
                  {content.type} • {formatFileSize(content.fileSizeBytes)}
                </span>
                <div className="flex space-x-2">
                  <button
                    onClick={() => {
                      setEditingContent(content)
                      setIsModalOpen(true)
                    }}
                    className="text-slate-600 hover:text-slate-900 text-sm"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(content.id)}
                    className="text-red-600 hover:text-red-900 text-sm"
                  >
                    Delete
                  </button>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>

      {data?.contents.length === 0 && (
        <div className="text-center py-12 text-slate-500">
          No content found. Add your first content to get started.
        </div>
      )}

      {isModalOpen && (
        <ContentModal
          content={editingContent}
          onClose={() => setIsModalOpen(false)}
        />
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
      content
        ? contentApi.update(content.id, data)
        : contentApi.create(data),
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
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg max-w-md w-full p-6">
        <h2 className="text-lg font-semibold mb-4">
          {content ? 'Edit Content' : 'Add Content'}
        </h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700">Name</label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Description</label>
            <input
              type="text"
              value={formData.description}
              onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Type</label>
            <select
              value={formData.type}
              onChange={(e) => setFormData({ ...formData, type: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
            >
              <option value="Image">Image</option>
              <option value="Video">Video</option>
              <option value="Html">HTML</option>
              <option value="Pdf">PDF</option>
              <option value="Url">URL</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">URL</label>
            <input
              type="url"
              value={formData.url}
              onChange={(e) => setFormData({ ...formData, url: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Thumbnail URL</label>
            <input
              type="url"
              value={formData.thumbnailUrl}
              onChange={(e) => setFormData({ ...formData, thumbnailUrl: e.target.value })}
              className="mt-1 block w-full px-3 py-2 border border-slate-300 rounded-md"
            />
          </div>
          <div className="flex justify-end space-x-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-slate-300 rounded-md text-slate-700 hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="px-4 py-2 bg-slate-900 text-white rounded-md hover:bg-slate-800 disabled:opacity-50"
            >
              {mutation.isPending ? 'Saving...' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
