import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { notificationChannelsApi, escalationApi } from '@/lib/api'
import type { NotificationChannelDto, EscalationPolicyDto } from '@/lib/api'
import { useState } from 'react'
import { Plus, Pencil, Trash2, BellRing, Send, Loader2, ArrowUp, ChevronDown, ChevronUp } from 'lucide-react'
import { clsx } from 'clsx'

const CHANNEL_TYPES = [
  { value: 'webhook', label: 'Webhook', placeholder: '{"url": "https://hooks.example.com/..."}' },
  { value: 'teams', label: 'Microsoft Teams', placeholder: '{"webhookUrl": "https://outlook.office.com/webhook/..."}' },
  { value: 'email', label: 'Email (SMTP)', placeholder: '{"to": "admin@example.com", "smtpHost": "smtp.example.com", "smtpPort": 587, "useSsl": true}' },
]

export function NotificationsPage() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingChannel, setEditingChannel] = useState<NotificationChannelDto | null>(null)
  const [testingId, setTestingId] = useState<string | null>(null)

  const { data: channels, isLoading, error } = useQuery({
    queryKey: ['notificationChannels'],
    queryFn: notificationChannelsApi.list,
  })

  const deleteMutation = useMutation({
    mutationFn: notificationChannelsApi.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notificationChannels'] }),
  })

  const testMutation = useMutation({
    mutationFn: notificationChannelsApi.test,
    onSuccess: () => setTestingId(null),
  })

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-white">Notification Channels</h1>
          <p className="mt-1 text-sm text-slate-400">Configure where alert notifications are sent</p>
        </div>
        <button
          onClick={() => { setEditingChannel(null); setIsModalOpen(true) }}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Add Channel
        </button>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-surface-700 border-t-accent-500" />
        </div>
      ) : error ? (
        <div className="mt-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Error: {error.message}
        </div>
      ) : channels?.length === 0 ? (
        <div className="mt-6 flex flex-col items-center rounded-xl border border-surface-800 bg-surface-900 py-12 shadow-lg">
          <BellRing className="h-8 w-8 text-slate-600" />
          <p className="mt-2 text-sm text-slate-500">No notification channels configured. Alerts will only appear in the dashboard.</p>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          {channels?.map((channel) => (
            <div key={channel.id} className="flex items-center justify-between rounded-xl border border-surface-800 bg-surface-900 px-5 py-4 shadow-lg">
              <div className="flex items-center gap-4">
                <div className={clsx(
                  'flex h-10 w-10 items-center justify-center rounded-lg',
                  channel.isEnabled ? 'bg-accent-500/10 text-accent-400' : 'bg-surface-800 text-slate-600'
                )}>
                  <BellRing className="h-5 w-5" />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-white">{channel.name}</span>
                    <span className="rounded-full bg-surface-800 px-2 py-0.5 text-xs text-slate-400">{channel.type}</span>
                    {!channel.isEnabled && (
                      <span className="rounded-full bg-red-500/10 px-2 py-0.5 text-xs text-red-400">Disabled</span>
                    )}
                  </div>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Created {new Date(channel.createdAt).toLocaleDateString()}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => { setTestingId(channel.id); testMutation.mutate(channel.id) }}
                  disabled={testingId === channel.id}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                >
                  {testingId === channel.id ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Send className="h-3.5 w-3.5" />
                  )}
                  Test
                </button>
                <button
                  onClick={() => { setEditingChannel(channel); setIsModalOpen(true) }}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                >
                  <Pencil className="h-3.5 w-3.5" />
                  Edit
                </button>
                <button
                  onClick={() => {
                    if (confirm(`Delete channel "${channel.name}"?`))
                      deleteMutation.mutate(channel.id)
                  }}
                  className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                  Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {isModalOpen && (
        <ChannelModal channel={editingChannel} onClose={() => setIsModalOpen(false)} />
      )}

      {/* ─── Escalation Policies ─── */}
      <EscalationSection />

      {testMutation.isSuccess && (
        <div className={clsx(
          'fixed bottom-4 right-4 rounded-lg px-4 py-3 text-sm shadow-lg',
          testMutation.data?.success
            ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30'
            : 'bg-red-500/10 text-red-400 border border-red-500/30'
        )}>
          {testMutation.data?.message}
        </div>
      )}
    </div>
  )
}

function EscalationSection() {
  const queryClient = useQueryClient()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [editingPolicy, setEditingPolicy] = useState<EscalationPolicyDto | null>(null)

  const { data: policies } = useQuery({
    queryKey: ['escalationPolicies'],
    queryFn: escalationApi.list,
  })

  const deleteMutation = useMutation({
    mutationFn: escalationApi.delete,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['escalationPolicies'] }),
  })

  return (
    <div className="mt-10">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Escalation Policies</h2>
          <p className="mt-1 text-sm text-slate-400">
            Define notification steps for unacknowledged alerts (e.g. notify team lead after 15 min, escalate to manager after 30 min)
          </p>
        </div>
        <button
          onClick={() => { setEditingPolicy(null); setIsModalOpen(true) }}
          className="flex items-center gap-2 rounded-lg bg-accent-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-accent-500/25 transition-colors hover:bg-accent-400"
        >
          <Plus className="h-4 w-4" />
          Add Policy
        </button>
      </div>

      {!policies || policies.length === 0 ? (
        <div className="mt-4 flex flex-col items-center rounded-xl border border-surface-800 bg-surface-900 py-8 shadow-lg">
          <ArrowUp className="h-8 w-8 text-slate-600" />
          <p className="mt-2 text-sm text-slate-500">No escalation policies configured. Alerts will use default notification behavior.</p>
        </div>
      ) : (
        <div className="mt-4 space-y-3">
          {policies.map((policy) => (
            <div key={policy.id} className="rounded-xl border border-surface-800 bg-surface-900 px-5 py-4 shadow-lg">
              <div className="flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-white">{policy.name}</span>
                    {!policy.isEnabled && (
                      <span className="rounded-full bg-red-500/10 px-2 py-0.5 text-xs text-red-400">Disabled</span>
                    )}
                    <span className="rounded-full bg-surface-800 px-2 py-0.5 text-xs text-slate-400">
                      {policy.steps.length} step{policy.steps.length !== 1 ? 's' : ''}
                    </span>
                  </div>
                  {policy.description && (
                    <p className="mt-0.5 text-xs text-slate-500">{policy.description}</p>
                  )}
                  {policy.steps.length > 0 && (
                    <div className="mt-2 space-y-1">
                      {policy.steps.map((step) => (
                        <div key={step.id} className="flex items-center gap-2 text-xs text-slate-400">
                          <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-surface-800 text-[10px] font-medium text-slate-500">
                            {step.order}
                          </span>
                          <span>After {step.delayMinutes} min → notify {step.channelName}</span>
                          {step.escalateSeverity && (
                            <span className="rounded bg-amber-500/10 px-1.5 py-0.5 text-[10px] text-amber-400">
                              escalate to {step.escalateSeverity}
                            </span>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => { setEditingPolicy(policy); setIsModalOpen(true) }}
                    className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-surface-800 hover:text-white"
                  >
                    <Pencil className="h-3.5 w-3.5" />
                    Edit
                  </button>
                  <button
                    onClick={() => {
                      if (confirm(`Delete policy "${policy.name}"?`))
                        deleteMutation.mutate(policy.id)
                    }}
                    className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-sm text-slate-400 transition-colors hover:bg-red-500/10 hover:text-red-400"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    Delete
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {isModalOpen && (
        <EscalationPolicyModal policy={editingPolicy} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}

function EscalationPolicyModal({
  policy,
  onClose,
}: {
  policy: EscalationPolicyDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(policy?.name ?? '')
  const [description, setDescription] = useState(policy?.description ?? '')
  const [isEnabled, setIsEnabled] = useState(policy?.isEnabled ?? true)
  const [steps, setSteps] = useState<Array<{ order: number; delayMinutes: number; channelId: string; escalateSeverity: string }>>(
    policy?.steps.map(s => ({
      order: s.order,
      delayMinutes: s.delayMinutes,
      channelId: s.channelId,
      escalateSeverity: s.escalateSeverity ?? '',
    })) ?? []
  )

  const { data: channels } = useQuery({
    queryKey: ['notificationChannels'],
    queryFn: notificationChannelsApi.list,
  })

  const mutation = useMutation({
    mutationFn: () => {
      const data = {
        name,
        description: description || undefined,
        isEnabled,
        steps: steps.map(s => ({
          order: s.order,
          delayMinutes: s.delayMinutes,
          channelId: s.channelId,
          escalateSeverity: s.escalateSeverity || undefined,
        })),
      }
      if (policy) {
        return escalationApi.update(policy.id, { ...data, isEnabled })
      }
      return escalationApi.create(data)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['escalationPolicies'] })
      onClose()
    },
  })

  const addStep = () => {
    const nextOrder = steps.length > 0 ? Math.max(...steps.map(s => s.order)) + 1 : 1
    setSteps([...steps, { order: nextOrder, delayMinutes: 15, channelId: channels?.[0]?.id ?? '', escalateSeverity: '' }])
  }

  const removeStep = (index: number) => {
    setSteps(steps.filter((_, i) => i !== index))
  }

  const moveStep = (index: number, direction: 'up' | 'down') => {
    const newSteps = [...steps]
    const target = direction === 'up' ? index - 1 : index + 1
    if (target < 0 || target >= newSteps.length) return
    ;[newSteps[index], newSteps[target]] = [newSteps[target], newSteps[index]]
    newSteps.forEach((s, i) => { s.order = i + 1 })
    setSteps(newSteps)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {policy ? 'Edit Escalation Policy' : 'Create Escalation Policy'}
        </h2>
        <form
          onSubmit={(e) => { e.preventDefault(); mutation.mutate() }}
          className="mt-4 space-y-4"
        >
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="e.g. Critical Alert Escalation"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-300">Description</label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="Optional description"
            />
          </div>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="policyEnabled"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.target.checked)}
              className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
            />
            <label htmlFor="policyEnabled" className="text-sm text-slate-300">Enabled</label>
          </div>

          {/* Steps */}
          <div>
            <div className="flex items-center justify-between">
              <label className="block text-sm font-medium text-slate-300">Escalation Steps</label>
              <button
                type="button"
                onClick={addStep}
                className="flex items-center gap-1 rounded-md px-2 py-1 text-xs text-accent-400 transition-colors hover:bg-surface-800"
              >
                <Plus className="h-3 w-3" /> Add Step
              </button>
            </div>
            {steps.length === 0 ? (
              <p className="mt-2 text-xs text-slate-500">No steps — alerts will use default notification behavior (immediate).</p>
            ) : (
              <div className="mt-2 space-y-2">
                {steps.map((step, i) => (
                  <div key={i} className="flex items-center gap-2 rounded-lg border border-surface-700 bg-surface-850 px-3 py-2">
                    <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-accent-500/10 text-xs font-medium text-accent-400">
                      {step.order}
                    </span>
                    <div className="flex flex-1 items-center gap-2">
                      <div>
                        <label className="block text-[10px] text-slate-500">Wait</label>
                        <input
                          type="number"
                          min={1}
                          value={step.delayMinutes}
                          onChange={(e) => {
                            const s = [...steps]
                            s[i].delayMinutes = parseInt(e.target.value) || 1
                            setSteps(s)
                          }}
                          className="w-16 rounded border border-surface-600 bg-surface-800 px-2 py-1 text-xs text-white outline-none"
                        />
                        <span className="ml-1 text-xs text-slate-500">min</span>
                      </div>
                      <div className="flex-1">
                        <label className="block text-[10px] text-slate-500">Notify via</label>
                        <select
                          value={step.channelId}
                          onChange={(e) => {
                            const s = [...steps]
                            s[i].channelId = e.target.value
                            setSteps(s)
                          }}
                          className="w-full rounded border border-surface-600 bg-surface-800 px-2 py-1 text-xs text-white outline-none"
                        >
                          {channels?.map(c => (
                            <option key={c.id} value={c.id}>{c.name} ({c.type})</option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label className="block text-[10px] text-slate-500">Escalate to</label>
                        <select
                          value={step.escalateSeverity}
                          onChange={(e) => {
                            const s = [...steps]
                            s[i].escalateSeverity = e.target.value
                            setSteps(s)
                          }}
                          className="rounded border border-surface-600 bg-surface-800 px-2 py-1 text-xs text-white outline-none"
                        >
                          <option value="">No change</option>
                          <option value="Warning">Warning</option>
                          <option value="Critical">Critical</option>
                        </select>
                      </div>
                    </div>
                    <div className="flex flex-col gap-0.5">
                      <button type="button" onClick={() => moveStep(i, 'up')} className="text-slate-500 hover:text-white">
                        <ChevronUp className="h-3 w-3" />
                      </button>
                      <button type="button" onClick={() => moveStep(i, 'down')} className="text-slate-500 hover:text-white">
                        <ChevronDown className="h-3 w-3" />
                      </button>
                    </div>
                    <button type="button" onClick={() => removeStep(i)} className="text-slate-500 hover:text-red-400">
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="flex justify-end gap-3">
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
          {mutation.isError && (
            <p className="text-sm text-red-400">{mutation.error.message}</p>
          )}
        </form>
      </div>
    </div>
  )
}

function ChannelModal({
  channel,
  onClose,
}: {
  channel: NotificationChannelDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(channel?.name ?? '')
  const [type, setType] = useState(channel?.type ?? 'webhook')
  const [configJson, setConfigJson] = useState(channel?.configJson ?? '')
  const [isEnabled, setIsEnabled] = useState(channel?.isEnabled ?? true)

  const selectedType = CHANNEL_TYPES.find(t => t.value === type)

  const mutation = useMutation({
    mutationFn: () => {
      if (channel) {
        return notificationChannelsApi.update(channel.id, { name, configJson, isEnabled })
      }
      return notificationChannelsApi.create({ name, type, configJson })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notificationChannels'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-xl border border-surface-700 bg-surface-900 p-6 shadow-2xl">
        <h2 className="text-lg font-semibold text-white">
          {channel ? 'Edit Channel' : 'Add Notification Channel'}
        </h2>
        <form
          onSubmit={(e) => { e.preventDefault(); mutation.mutate() }}
          className="mt-4 space-y-4"
        >
          <div>
            <label className="block text-sm font-medium text-slate-300">Name</label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              placeholder="e.g. Ops Team Webhook"
            />
          </div>
          {!channel && (
            <div>
              <label className="block text-sm font-medium text-slate-300">Type</label>
              <select
                value={type}
                onChange={(e) => setType(e.target.value)}
                className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 text-sm text-white outline-none focus:border-accent-500"
              >
                {CHANNEL_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
          )}
          <div>
            <label className="block text-sm font-medium text-slate-300">Configuration (JSON)</label>
            <textarea
              required
              value={configJson}
              onChange={(e) => setConfigJson(e.target.value)}
              rows={4}
              className="mt-1 w-full rounded-lg border border-surface-700 bg-surface-850 px-3 py-2 font-mono text-sm text-white outline-none focus:border-accent-500"
              placeholder={selectedType?.placeholder}
            />
          </div>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isEnabled"
              checked={isEnabled}
              onChange={(e) => setIsEnabled(e.target.checked)}
              className="h-4 w-4 rounded border-surface-600 bg-surface-800 text-accent-500 focus:ring-accent-500"
            />
            <label htmlFor="isEnabled" className="text-sm text-slate-300">Enabled</label>
          </div>
          <div className="flex justify-end gap-3">
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
          {mutation.isError && (
            <p className="text-sm text-red-400">{mutation.error.message}</p>
          )}
        </form>
      </div>
    </div>
  )
}
