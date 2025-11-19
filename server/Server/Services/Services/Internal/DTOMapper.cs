using Data.Model;
using Services.Contracts.DTOs;

namespace Services.Services.Internal
{
    public static class DTOMapper
    {
        public static PasswordWordDTO ToWordDTO(PasswordWord entity)
        {
            if (entity == null)
            {
                return new PasswordWordDTO { SpanishWord = "END", EnglishWord = "END" };
            }
            return new PasswordWordDTO
            {
                EnglishWord = entity.EnglishWord,
                SpanishWord = entity.SpanishWord,
                EnglishDescription = entity.EnglishDescription,
                SpanishDescription = entity.SpanishDescription
            };
        }

        public static PasswordWordDTO ToMaskedWordDTO(PasswordWord entity)
        {
            if (entity == null)
            {
                return new PasswordWordDTO { SpanishWord = "END", EnglishWord = "END" };
            }
            return new PasswordWordDTO
            {
                EnglishWord = string.Empty, 
                SpanishWord = string.Empty, 
                EnglishDescription = entity.EnglishDescription,
                SpanishDescription = entity.SpanishDescription
            };
        }
    }
}
