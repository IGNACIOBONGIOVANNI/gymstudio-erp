using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace EstudioContable.Application.Common.Behaviors
{
    /// <summary>
    /// Ejecuta los validadores de FluentValidation del comando antes de llegar al handler.
    /// Si alguno falla, lanza ValidationException (el middleware la traduce a HTTP 400)
    /// y el handler no se ejecuta.
    /// </summary>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_validators.Any())
            {
                var contexto = new ValidationContext<TRequest>(request);
                var resultados = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(contexto, cancellationToken)));
                var errores = resultados.SelectMany(r => r.Errors).Where(e => e != null).ToList();

                if (errores.Count > 0)
                    throw new ValidationException(errores);
            }

            return await next(cancellationToken);
        }
    }
}
