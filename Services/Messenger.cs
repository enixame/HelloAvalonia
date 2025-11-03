using System;
using System.Collections.Generic;

namespace HelloAvalonia.Services;

/// <summary>
/// Service de messagerie pour communication découplée entre ViewModels
/// </summary>
public class Messenger
{
    private static Messenger? _instance;
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    public static Messenger Default => _instance ??= new Messenger();

    /// <summary>
    /// S'abonne à un type de message
    /// </summary>
    public void Subscribe<TMessage>(Action<TMessage> action)
    {
        var messageType = typeof(TMessage);
        
        if (!_subscribers.ContainsKey(messageType))
        {
            _subscribers[messageType] = new List<Delegate>();
        }

        _subscribers[messageType].Add(action);
    }

    /// <summary>
    /// Se désabonne d'un type de message
    /// </summary>
    public void Unsubscribe<TMessage>(Action<TMessage> action)
    {
        var messageType = typeof(TMessage);
        
        if (_subscribers.ContainsKey(messageType))
        {
            _subscribers[messageType].Remove(action);
        }
    }

    /// <summary>
    /// Envoie un message à tous les abonnés
    /// </summary>
    public void Send<TMessage>(TMessage message)
    {
        var messageType = typeof(TMessage);
        
        if (_subscribers.ContainsKey(messageType))
        {
            foreach (var subscriber in _subscribers[messageType].ToArray())
            {
                ((Action<TMessage>)subscriber).Invoke(message);
            }
        }
    }
}
