using System;
using KMRLauncherMvvm.Models;

namespace KMRLauncherMvvm.Exceptions;

public class InvalidRelationshipException : Exception
{
    public override string Message { get; }

    public InvalidRelationshipException(Relationship relationship)
    {
        Message =
            $"Relationship has an invalid combination of properties: {{ Name: {relationship.Name}, " +
            $"MinVersion: {relationship.MinVersion}, " +
            $"MaxVersion: {relationship.MaxVersion}, " +
            $"Version: {relationship.Version}, " +
            $"Comment: {relationship.Comment}, " +
            $"SuppressRecommendations: {relationship.SuppressRecommendations}}}";
    }
}