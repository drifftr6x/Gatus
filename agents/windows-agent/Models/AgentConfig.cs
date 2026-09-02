namespace SentinelKiosk.Agent.Models;

public class AgentConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5163";
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int PolicySyncIntervalSeconds { get; set; } = 300;
    public int DeploymentCheckIntervalSeconds { get; set; } = 60;
    public int CommandPollIntervalSeconds { get; set; } = 15;
    public int TelemetryBatchSize { get; set; } = 100;
    public int TelemetryUploadIntervalSeconds { get; set; } = 300;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartDelaySeconds { get; set; } = 5;
}

public class DeviceCredentials
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceSecret { get; set; } = string.Empty;
    public string? EnrollmentToken { get; set; }
    public DateTime EnrolledAt { get; set; }
    public string? CertificateThumbprint { get; set; }
}

public class SigningKeyPin
{
    public string PublicKey { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
}

public class AgentState
{
    public string DeviceId { get; set; } = string.Empty;
    public string Status { get; set; } = "Offline";
    public string? CurrentPolicyVersion { get; set; }
    public string? CurrentContentVersion { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime LastPolicySync { get; set; }
    public DateTime LastDeploymentCheck { get; set; }
    public string? CredentialStatus { get; set; }
    public DateTime? CredentialRejectedAt { get; set; }
    public List<PendingTelemetry> PendingTelemetry { get; set; } = [];
    public List<string> PendingCommands { get; set; } = [];
}

public class PendingTelemetry
{
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public string MetricValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
}
