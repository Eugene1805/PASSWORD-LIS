using Data.Model;
using Services.Contracts.DTOs;

namespace Services.Services.Internal
{
    public static class DTOMapper
    {
        private const int InvalidPasswordId = -1;
        private const string EndWord = "END";
        public static PasswordWordDTO ToWordDTO(PasswordWord entity)
        {
            if (entity == null || entity.Id == InvalidPasswordId)
            {
                return new PasswordWordDTO { SpanishWord = EndWord, EnglishWord = EndWord };
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
            if (entity == null || entity.Id == InvalidPasswordId)
            {
                return new PasswordWordDTO { SpanishWord = EndWord, EnglishWord = EndWord };
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
