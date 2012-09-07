﻿//-----------------------------------------------------------------------
// <copyright>
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Katana.Server.HttpListenerWrapper
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RequestLifetimeMonitor : IDisposable
    {
        private const int RequestInProgress = 1;
        private const int ResponseInProgress = 2;
        private const int Completed = 3;

        private HttpListenerContext context;
        private CancellationTokenSource cts;
        private int requestState;
        private Timer timeout;

        internal RequestLifetimeMonitor(HttpListenerContext context, TimeSpan timeLimit)
        {
            this.context = context;
            this.cts = new CancellationTokenSource();
            // .NET 4.5: cts.CancelAfter(timeLimit);
            this.timeout = new Timer(Cancel, this, timeLimit, TimeSpan.FromMilliseconds(Timeout.Infinite));
            this.requestState = RequestInProgress;
        }

        internal CancellationToken Token
        {
            get
            {
                return this.cts.Token;
            }
        }

        internal bool TryStartResponse()
        {
            return Interlocked.CompareExchange(ref this.requestState, ResponseInProgress, RequestInProgress) == RequestInProgress;
        }

        private static void Cancel(object state)
        {
            RequestLifetimeMonitor monitor = (RequestLifetimeMonitor)state;
            monitor.End(new TimeoutException());
        }

        // The request completed successfully.  Cancel the token anyways so any registered listeners can do cleanup.
        internal void CompleteResponse()
        {
            try
            {
                this.context.Response.Close();
                this.End(null);
            }
            catch (HttpListenerException ex)
            {
                // TODO: Log
                this.End(ex);
            }
        }

        internal void End(Exception ex)
        {
            // Debug.Assert(false, "Request exception: " + ex.ToString());
            
            if (ex != null)
            {
                // TODO: LOG

                try
                {
                    this.cts.Cancel();
                }
                catch (ObjectDisposedException)
                { 
                }
                catch (AggregateException)
                {
                    // TODO: LOG
                }
            }

            this.End();
        }

        private void End()
        {
            this.timeout.Dispose();
            this.cts.Dispose();
            int priorState = Interlocked.Exchange(ref this.requestState, Completed);

            if (priorState == RequestInProgress)
            {
                // If the response has not started yet then we can send an error response before closing it.
                this.context.Response.StatusCode = 500;
                this.context.Response.ContentLength64 = 0;
                this.context.Response.Headers.Clear();
                this.context.Response.Close();
            }
            else if (priorState == ResponseInProgress)
            {
                this.context.Response.Abort();
            }
            else
            {
                Contract.Requires(priorState == Completed);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                End();
            }
        }
    }
}