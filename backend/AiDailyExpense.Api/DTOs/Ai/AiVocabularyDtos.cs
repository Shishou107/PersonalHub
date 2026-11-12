using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiDailyExpense.Api.DTOs.Ai
{
    public class AiVocabularyRequest
    {
        [Required(ErrorMessage = "Vui long nhap tu vung.")]
        [StringLength(200, ErrorMessage = "Tu vung toi da 200 ky tu.")]
        public string Text { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Luu y cho AI toi da 500 ky tu.")]
        public string? Instruction { get; set; }
    }

    public class AiVocabularyResponse
    {
        public string Word { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
        public string PartOfSpeech { get; set; } = string.Empty;
        public string Pronunciation { get; set; } = string.Empty;
        public string Example { get; set; } = string.Empty;
        public string ExampleTranslation { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class AiVocabularyAnalyzeRequest
    {
        [Required(ErrorMessage = "Vui long chon bo the.")]
        public int DeckId { get; set; }

        [Required(ErrorMessage = "Vui long nhap tu vung.")]
        [StringLength(200, ErrorMessage = "Tu vung toi da 200 ky tu.")]
        public string Word { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Luu y cho AI toi da 500 ky tu.")]
        public string? Instruction { get; set; }
    }

    public class AiVocabularyAnalyzeResponse
    {
        public string Language { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        [JsonPropertyName("meaning_vi")]
        public string MeaningVi { get; set; } = string.Empty;
        [JsonPropertyName("base_word")]
        public string BaseWord { get; set; } = string.Empty;
        [JsonPropertyName("word_family")]
        public AiVocabularyWordFamilyDto WordFamily { get; set; } = new();
        public List<AiVocabularySynonymDto> Synonyms { get; set; } = new();
    }

    public class AiVocabularyWordFamilyDto
    {
        public List<string> Noun { get; set; } = new();
        public List<string> Verb { get; set; } = new();
        public List<string> Adjective { get; set; } = new();
        public List<string> Adverb { get; set; } = new();
    }

    public class AiVocabularySynonymDto
    {
        public string Word { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        [JsonPropertyName("meaning_vi")]
        public string MeaningVi { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}
