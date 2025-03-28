using DotPulsar;

namespace Wolverine.Pulsar;

/// <summary>
/// TODO: how to handle retries internally in Wolverine?
/// </summary>
public class RetryLetterTopic
{
    public static RetryLetterTopic DefaultNative => new([
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2)
    ]);


    /// <summary>
    /// Message delaying does not work with Pulsar if the subscription type is not shared or key shared
    /// </summary>
    public static IReadOnlySet<SubscriptionType> SupportedSubscriptionTypes = new HashSet<SubscriptionType>()
    {
        SubscriptionType.Shared, SubscriptionType.KeyShared
    };

    private string? _topicName;
    private readonly List<TimeSpan> _retries;

    public RetryLetterTopic(List<TimeSpan> retries)
    {
        _retries = retries;
    } 
    public RetryLetterTopic(string topicName, List<TimeSpan> retries)
    {
        _topicName = topicName;
        _retries = retries;
    }

    public string? TopicName
    {
        get => _topicName;
        set => _topicName = value ?? throw new ArgumentNullException(nameof(TopicName));
    }

    public List<TimeSpan> Retry => _retries.ToList();

    protected bool Equals(RetryLetterTopic other)
    {
        return _topicName == other._topicName;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((RetryLetterTopic)obj);
    }

    public override int GetHashCode()
    {
        return _topicName.GetHashCode();
    }
}