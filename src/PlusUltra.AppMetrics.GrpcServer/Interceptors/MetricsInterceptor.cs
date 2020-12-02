using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using App.Metrics;


namespace PlusUltra.AppMetrics.GrpcServer.Interpectors
{
    /// <summary>
    /// Interceptor for intercepting calls on server side 
    /// </summary>
    public class ServerMetricsInterceptor : Interceptor
    {

        
        /// <summary>
        /// Constructor for server side interceptor
        /// </summary>
        /// <param name="enableLatencyMetrics">Set if latency metrics is enabled</param>
        public ServerMetricsInterceptor(IMetrics metrics, bool enableLatencyMetrics = false)
        {
            this.enableLatencyMetrics = enableLatencyMetrics;
            this.metrics = metrics;
        }
        private readonly bool enableLatencyMetrics;
        private readonly IMetrics metrics; 

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            GrpcMethodInfo method = new GrpcMethodInfo(context.Method, MethodType.Unary);

            metrics.RequestCounterInc(method);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            try
            {
                TResponse result = await continuation(request, context);
                metrics.ResponseCounterInc(method, context.Status.StatusCode);
                return result;
            }
            catch (RpcException e)
            {
                metrics.ResponseCounterInc(method, e.Status.StatusCode);
                throw;
            }
            finally
            {
                watch.Stop();
                if (enableLatencyMetrics)
                    metrics.RecordLatency(method, watch.Elapsed.TotalSeconds);
            }
        }

        public override Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            GrpcMethodInfo method = new GrpcMethodInfo(context.Method, MethodType.ServerStreaming);

            metrics.RequestCounterInc(method);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            Task result;

            try
            {
                result = continuation(request,
                    new WrapperServerStreamWriter<TResponse>(responseStream,
                        () => { metrics.StreamSentCounterInc(method); }),
                    context);

                metrics.ResponseCounterInc(method, StatusCode.OK);
            }
            catch (RpcException e)
            {
                metrics.ResponseCounterInc(method, e.Status.StatusCode);
                throw;
            }
            finally
            {
                watch.Stop();

                if (enableLatencyMetrics)
                    metrics.RecordLatency(method, watch.Elapsed.TotalSeconds);
            }

            return result;
        }

        public override Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            GrpcMethodInfo method = new GrpcMethodInfo(context.Method, MethodType.ClientStreaming);

            metrics.RequestCounterInc(method);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            Task<TResponse> result;

            try
            {
                result = continuation(
                    new WrapperStreamReader<TRequest>(requestStream,
                        () => { metrics.StreamReceivedCounterInc(method); }), context);

                metrics.ResponseCounterInc(method, StatusCode.OK);
            }
            catch (RpcException e)
            {
                metrics.ResponseCounterInc(method, e.Status.StatusCode);
                throw;
            }
            finally
            {
                watch.Stop();
                if (enableLatencyMetrics)
                    metrics.RecordLatency(method, watch.Elapsed.TotalSeconds);
            }

            return result;
        }

        public override Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            GrpcMethodInfo method = new GrpcMethodInfo(context.Method, MethodType.DuplexStreaming);

            metrics.RequestCounterInc(method);

            Stopwatch watch = new Stopwatch();
            watch.Start();

            Task result;

            try
            {
                result = continuation(
                    new WrapperStreamReader<TRequest>(requestStream,
                        () => { metrics.StreamReceivedCounterInc(method); }),
                    new WrapperServerStreamWriter<TResponse>(responseStream,
                        () => { metrics.StreamSentCounterInc(method); }), context);

                metrics.ResponseCounterInc(method, StatusCode.OK);

            }
            catch (RpcException e)
            {
                metrics.ResponseCounterInc(method, e.Status.StatusCode);

                throw;
            }
            finally
            {
                watch.Stop();
                if (enableLatencyMetrics)
                    metrics.RecordLatency(method, watch.Elapsed.TotalSeconds);
            }

            return result;
        }
    }
}