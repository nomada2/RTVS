﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Common.Core;
using Microsoft.R.Actions.Logging;
using Microsoft.R.Support.Settings.Definitions;

namespace Microsoft.VisualStudio.R.Package.SurveyNews {
    [Export(typeof(ISurveyNewsService))]
    internal class SurveyNewsService : ISurveyNewsService {
        private ISurveyNewsFeedClient _feedClient;
        private ISurveyNewsOptions _options;
        private ISurveyNewsBrowserLauncher _browserLauncher;

        [ImportingConstructor]
        public SurveyNewsService(ISurveyNewsFeedClient feedClient, ISurveyNewsOptions options, ISurveyNewsBrowserLauncher browserLauncher) {
            _feedClient = feedClient;
            _options = options;
            _browserLauncher = browserLauncher;
        }

        public async Task CheckSurveyNewsAsync(bool forceCheck) {
            bool shouldQueryServer = false;
            if (forceCheck) {
                shouldQueryServer = true;
            } else {
                // Ensure that we don't prompt the user on their very first project creation.
                // Delay by 3 days by pretending we checked 4 days ago (the default policy is
                // once a week, so we'll check again in 3 days).
                // Note that if the user changed the policy to once a day before ever opening a R project
                // then we'll end up checking with the server, which is fine.
                if (_options.SurveyNewsLastCheck == DateTime.MinValue) {
                    _options.SurveyNewsLastCheck = DateTime.Now - TimeSpan.FromDays(4);
                }

                shouldQueryServer = ShouldQueryServer();
            }

            if (shouldQueryServer) {
                await QueryServerAsync(forceCheck);
            }
        }

        private bool ShouldQueryServer() {
            bool shouldQuery = false;

            var elapsedTime = DateTime.Now - _options.SurveyNewsLastCheck;
            switch (_options.SurveyNewsCheck) {
                case SurveyNewsPolicy.Disabled:
                    break;
                case SurveyNewsPolicy.CheckOnceDay:
                    shouldQuery = elapsedTime.TotalDays >= 1;
                    break;
                case SurveyNewsPolicy.CheckOnceWeek:
                    shouldQuery = elapsedTime.TotalDays >= 7;
                    break;
                case SurveyNewsPolicy.CheckOnceMonth:
                    shouldQuery = elapsedTime.TotalDays >= 30;
                    break;
                default:
                    Debug.Assert(false, String.Format("Unexpected SurveyNewsPolicy: {0}.", _options.SurveyNewsCheck));
                    break;
            }

            return shouldQuery;
        }

        private async Task QueryServerAsync(bool forceCheck) {
            _options.SurveyNewsLastCheck = DateTime.Now;

            string url = null;
            try {
                var feed = await _feedClient.GetFeedAsync(_options.FeedUrl);
                if (feed?.NotVotedUrls?.Length > 0) {
                    url = feed.NotVotedUrls[0];
                } else if (forceCheck) {
                    url = _options.IndexUrl;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                GeneralLog.Write(ex);
                if (forceCheck) {
                    url = _options.CannotConnectUrl;
                }
            }

            try {
                if (!string.IsNullOrEmpty(url)) {
                    if (forceCheck) {
                        _browserLauncher.Navigate(url);
                    } else {
                        _browserLauncher.NavigateOnIdle(url);
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                GeneralLog.Write(ex);
            }
        }
    }
}
