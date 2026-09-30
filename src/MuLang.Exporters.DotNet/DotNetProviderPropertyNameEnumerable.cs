using System.Collections;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetProviderPropertyNameEnumerable : IEnumerable<string>
{
    private readonly DotNetProviderInvocationContext context;
    private readonly IEnumerable<string> names;

    public DotNetProviderPropertyNameEnumerable(
        DotNetProviderInvocationContext context,
        IEnumerable<string> names
    )
    {
        this.context = context;
        this.names = names;
    }

    public IEnumerator<string> GetEnumerator()
    {
        context.EnsureEnumerationActive();
        return new Enumerator(context, names.GetEnumerator());
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private sealed class Enumerator : IEnumerator<string>
    {
        private readonly DotNetProviderInvocationContext context;
        private readonly IEnumerator<string> names;

        public Enumerator(
            DotNetProviderInvocationContext context,
            IEnumerator<string> names
        )
        {
            this.context = context;
            this.names = names;
        }

        public string Current
        {
            get
            {
                context.EnsureEnumerationActive();
                return names.Current;
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            context.ConsumeEnumerationStep();
            return names.MoveNext();
        }

        public void Reset()
        {
            context.ConsumeEnumerationStep();
            names.Reset();
        }

        public void Dispose()
        {
            names.Dispose();
        }
    }
}
