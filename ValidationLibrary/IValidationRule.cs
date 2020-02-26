﻿using System;
using System.Threading.Tasks;
using Octokit;

namespace ValidationLibrary
{
    /// <summary>
    /// Common interface for all validation rules
    /// </summary>
    public interface IValidationRule
    {
        string RuleName { get; }

        Task Init(IGitHubClient ghClient);

        Task<ValidationResult> IsValid(IGitHubClient client, Repository repository);
    }
}
