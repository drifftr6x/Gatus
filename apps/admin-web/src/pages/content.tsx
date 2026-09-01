import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { contentApi, deploymentsApi, groupsApi, devicesApi } from '@/lib/api'
import type { ContentDto } from '@/lib/api'
import { useState, useRef } from 'react'
import { Plus, Pencil, Trash2, Image, Video, FileText, Globe, FileCode, Upload, Rocket } from 'lucide-react'

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
  const [isUploadOpen, setIsUploadOpen] = useState(false)
  const [editingContent, setEditingContent] = useState<ContentDto | null>(null)
  const [deployContent, setDeployContent] = useState<ContentDto | null>(null)

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
        <div className="flex gap-2">
          <button
            onClick={() => setIsUploadOpen(true)}
            className="flex items-center gap-2 rounded-lg border border-surface-700 px-4 py-2 text-sm font-medium text-slate-200 transition-colors hover:bg-surface-800"
          >
            <Upload className="h-4 w-4" />
            Upload File
          </button>
          <button
            onClick={() => { setEditingContent(null); setIsModalOpen(true) }}
            className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
          >
            <Plus className="h-4 w-4" />
            Add Content
          </button>
        </div>
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
                    <img src={content.thumbnailUrl} alt={content.name} className="h-full w-full object-cover" />
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
                        onClick={() => setDeployContent(content)}
                        className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-emerald-500/10 hover:text-emerald-400"
                        title="Deploy to devices"
                      >
                        <Rocket className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => { setEditingContent(content); setIsModalOpen(true) }}
                        className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                        title="Edit"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleDelete(content.id)}
                        className="rounded-md p-1.5 text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                        title="Delete"
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
          No content found. Upload a file or add content to get started.
        </div>
      )}

      {isModalOpen && (
        <ContentModal content={editingContent} onClose={() => setIsModalOpen(false)} />
      )}
      {isUploadOpen && (
        <UploadModal onClose={() => setIsUploadOpen(false)} />
      )}
      {deployContent && (
        <DeployModal content={deployContent} onClose={() => setDeployContent(null)} />
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

// ── Upload Modal ────────────────────────────────────────────────

function UploadModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')

  const uploadMutation = useMutation({
    mutationFn: () => contentApi.upload(file!, name, description || undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['content'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">Upload Content</h2>
        <form
          onSubmit={(e) => { e.preventDefault(); if (file) uploadMutation.mutate() }}
          className="mt-5 space-y-4"
        >
          <div>
            <label className="block text-sm font-medium text-slate-300">File</label>
            <input ref={fileRef} type="file" accept="image/*,video/*,.pdf,.html,.htm"
              onChange={(e) => {
                const f = e.target.files?.[0]
                if (f) { setFile(f); if (!name) setName(f.name.replace(/\.[^.]+$/, '')) }
              }}
              className="mt-1.5 block w-full text-sm text-slate-400 file:mr-4 file:rounded-lg file:border-0 file:bg-accent-500 file:px-4 file:py-2 file:text-sm file:font-medium file:text-white hover:file:bg-accent-400"
            />
            {file && <p className="mt-1 text-xs text-slate-500">{file.name} ({formatFileSize(file.size)})</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input type="text" value={name} onChange={(e) => setName(e.target.value)} className={inputClass} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input type="text" value={description} onChange={(e) => setDescription(e.target.value)} className={inputClass} />
          </div>
          <div className="mt-6 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800">Cancel</button>
            <button type="submit" disabled={!file || uploadMutation.isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 hover:bg-accent-400 disabled:opacity-50">
              {uploadMutation.isPending ? 'Uploading…' : 'Upload'}
            </button>
          </div>
          {uploadMutation.isError && <p className="text-sm text-red-400">{uploadMutation.error.message}</p>}
        </form>
      </div>
    </div>
  )
}

// ── Deploy Modal ────────────────────────────────────────────────

function DeployModal({ content, onClose }: { content: ContentDto; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [targetMode, setTargetMode] = useState<'group' | 'devices'>('group')
  const [selectedGroupId, setSelectedGroupId] = useState('')
  const [selectedDeviceIds, setSelectedDeviceIds] = useState<Set<string>>(new Set())
  const [deployName, setDeployName] = useState(`Deploy ${content.name}`)
  const [scheduledAt, setScheduledAt] = useState('')
  const [rolloutPercent, setRolloutPercent] = useState<number | ''>('')

  const { data: versions } = useQuery({
    queryKey: ['content-versions', content.id],
    queryFn: () => contentApi.versions(content.id),
  })

  const { data: groups } = useQuery({
    queryKey: ['deviceGroups'],
    queryFn: groupsApi.list,
  })

  const { data: allDevices } = useQuery({
    queryKey: ['devices', 'all'],
    queryFn: () => devicesApi.list({ pageSize: 500 }),
  })

  const latestVersion = versions?.[0] // Ordered desc by version

  const deployMutation = useMutation({
    mutationFn: () => {
      if (!latestVersion) throw new Error('No content version available')
      return deploymentsApi.create({
        contentVersionId: latestVersion.id,
        name: deployName,
        groupId: targetMode === 'group' ? selectedGroupId || undefined : undefined,
        deviceIds: targetMode === 'devices' ? [...selectedDeviceIds] : undefined,
        scheduledAt: scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
        rolloutPercent: rolloutPercent !== '' ? rolloutPercent : undefined,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['deployments'] })
      onClose()
    },
  })

  const toggleDevice = (id: string) => {
    const next = new Set(selectedDeviceIds)
    if (next.has(id)) next.delete(id); else next.add(id)
    setSelectedDeviceIds(next)
  }

  const canDeploy = latestVersion && (
    (targetMode === 'group' && selectedGroupId) ||
    (targetMode === 'devices' && selectedDeviceIds.size > 0)
  )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">Deploy Content</h2>
        <p className="mt-1 text-sm text-slate-400">{content.name}</p>

        <div className="mt-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Deployment Name</label>
            <input type="text" value={deployName} onChange={(e) => setDeployName(e.target.value)} className={inputClass} />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300">Version</label>
            <div className="mt-1.5 rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-slate-300">
              {latestVersion ? (
                <span>v{latestVersion.version} — {formatFileSize(latestVersion.fileSizeBytes)} — {latestVersion.sha256Checksum.slice(0, 12)}…</span>
              ) : (
                <span className="text-amber-400">No versions available. Upload a file first.</span>
              )}
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300">Target</label>
            <div className="mt-2 flex gap-2">
              <button
                type="button"
                onClick={() => setTargetMode('group')}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  targetMode === 'group' ? 'bg-accent-500 text-white' : 'border border-surface-700 text-slate-400 hover:bg-surface-800'
                }`}
              >
                By Group
              </button>
              <button
                type="button"
                onClick={() => setTargetMode('devices')}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  targetMode === 'devices' ? 'bg-accent-500 text-white' : 'border border-surface-700 text-slate-400 hover:bg-surface-800'
                }`}
              >
                By Device
              </button>
            </div>

            {targetMode === 'group' ? (
              <select value={selectedGroupId} onChange={(e) => setSelectedGroupId(e.target.value)} className={`${inputClass} mt-3`}>
                <option value="">Select a group…</option>
                {groups?.filter(g => g.deviceCount > 0).map((g) => (
                  <option key={g.id} value={g.id}>{g.name} ({g.deviceCount} devices)</option>
                ))}
              </select>
            ) : (
              <div className="mt-3 max-h-40 overflow-y-auto rounded-lg border border-surface-700">
                {allDevices?.devices.map((d) => (
                  <label key={d.id} className={`flex cursor-pointer items-center gap-3 border-b border-surface-800 px-3 py-2 last:border-0 ${selectedDeviceIds.has(d.id) ? 'bg-accent-500/10' : 'hover:bg-surface-850'}`}>
                    <input type="checkbox" checked={selectedDeviceIds.has(d.id)} onChange={() => toggleDevice(d.id)}
                      className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500" />
                    <span className="text-sm text-white">{d.name}</span>
                    <span className="text-xs text-slate-500">{d.groupName || ''}</span>
                  </label>
                ))}
              </div>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300">Schedule</label>
            <input
              type="datetime-local"
              value={scheduledAt}
              onChange={(e) => setScheduledAt(e.target.value)}
              className={`${inputClass} mt-1.5`}
            />
            <p className="mt-1 text-xs text-slate-500">Leave empty to deploy immediately</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-300">Rollout Wave</label>
            <select
              value={rolloutPercent}
              onChange={(e) => setRolloutPercent(e.target.value === '' ? '' : Number(e.target.value))}
              className={`${inputClass} mt-1.5`}
            >
              <option value="">All devices at once</option>
              <option value={25}>25% first wave</option>
              <option value={50}>50% first wave</option>
              <option value={10}>10% first wave (canary)</option>
            </select>
            <p className="mt-1 text-xs text-slate-500">Gradual rollout doubles each wave after success</p>
          </div>

          <div className="mt-6 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800">Cancel</button>
            <button
              onClick={() => deployMutation.mutate()}
              disabled={!canDeploy || deployMutation.isPending}
              className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-emerald-500/25 hover:bg-emerald-500 disabled:opacity-50"
            >
              {deployMutation.isPending ? 'Deploying…' : scheduledAt ? 'Schedule Deploy' : 'Deploy'}
            </button>
          </div>
          {deployMutation.isError && <p className="text-sm text-red-400">{deployMutation.error.message}</p>}
        </div>
      </div>
    </div>
  )
}

// ── Content Modal (edit metadata) ───────────────────────────────

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

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {content ? 'Edit Content' : 'Add Content'}
        </h2>
        <form onSubmit={(e) => { e.preventDefault(); mutation.mutate(formData) }} className="mt-5 space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input type="text" value={formData.name} onChange={(e) => setFormData({ ...formData, name: e.target.value })} className={inputClass} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input type="text" value={formData.description} onChange={(e) => setFormData({ ...formData, description: e.target.value })} className={inputClass} />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Type</label>
            <select value={formData.type} onChange={(e) => setFormData({ ...formData, type: e.target.value })} className={inputClass}>
              <option value="Image">Image</option>
              <option value="Video">Video</option>
              <option value="Html">HTML</option>
              <option value="Pdf">PDF</option>
              <option value="Url">URL</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">URL</label>
            <input type="url" value={formData.url} onChange={(e) => setFormData({ ...formData, url: e.target.value })} className={inputClass} required />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Thumbnail URL</label>
            <input type="url" value={formData.thumbnailUrl} onChange={(e) => setFormData({ ...formData, thumbnailUrl: e.target.value })} className={inputClass} />
          </div>
          <div className="mt-6 flex justify-end gap-3">
            <button type="button" onClick={onClose} className="rounded-lg border border-surface-700 px-4 py-2 text-sm text-slate-300 hover:bg-surface-800">Cancel</button>
            <button type="submit" disabled={mutation.isPending}
              className="rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 hover:bg-accent-400 disabled:opacity-50">
              {mutation.isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
          {mutation.isError && <p className="text-sm text-red-400">{mutation.error.message}</p>}
        </form>
      </div>
    </div>
  )
}
