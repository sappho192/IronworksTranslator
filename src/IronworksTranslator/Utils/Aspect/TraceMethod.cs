using ArxOne.MrAdvice.Advice;
using Serilog;

namespace IronworksTranslator.Utils.Aspect
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class TraceMethod : Attribute, IMethodAdvice
    {
        public void Advise(MethodAdviceContext context)
        {
            Log.Information($"→ {context.TargetName}()");
            context.Proceed();
            Log.Information($"← {context.TargetName}()");
        }
    }
}
