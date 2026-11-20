using System;
using System.Text.Json;
using AiDailyExpense.Api.DTOs.Ai;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class VocabularyFormsAndDuplicateTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [Fact]
        public void AiVocabularyAnalyzeResponse_DeserializesForms_Correctly()
        {
            var json = """
            {
              "language": "english",
              "input": "ran",
              "base_word": "run",
              "meaning_vi": "chạy",
              "word_family": {
                "noun": ["runner"],
                "verb": ["run"],
                "adjective": []
              },
              "forms": {
                "plural": null,
                "v2": "ran",
                "v3": "run",
                "v_ing": "running",
                "v_s_es": "runs",
                "comparative": null,
                "superlative": null
              },
              "synonyms": []
            }
            """;

            var response = JsonSerializer.Deserialize<AiVocabularyAnalyzeResponse>(
                json,
                JsonOptions);

            Assert.NotNull(response);
            Assert.Equal("run", response.BaseWord);
            Assert.NotNull(response.Forms);
            Assert.Equal("ran", response.Forms.V2);
            Assert.Equal("run", response.Forms.V3);
            Assert.Equal("running", response.Forms.VIng);
            Assert.Equal("runs", response.Forms.VSes);
            Assert.Null(response.Forms.Plural);
            Assert.Null(response.Forms.Comparative);
        }

        [Fact]
        public void AiVocabularyAnalyzeResponse_DeserializesNounForms_Correctly()
        {
            var json = """
            {
              "language": "english",
              "input": "children",
              "base_word": "child",
              "meaning_vi": "đứa trẻ",
              "forms": {
                "plural": "children",
                "v2": null,
                "v3": null
              }
            }
            """;

            var response = JsonSerializer.Deserialize<AiVocabularyAnalyzeResponse>(
                json,
                JsonOptions);

            Assert.NotNull(response);
            Assert.Equal("child", response.BaseWord);
            Assert.NotNull(response.Forms);
            Assert.Equal("children", response.Forms.Plural);
            Assert.Null(response.Forms.V2);
        }

        [Fact]
        public void AiVocabularyAnalyzeResponse_DeserializesAdjectiveForms_Correctly()
        {
            var json = """
            {
              "language": "english",
              "input": "happier",
              "base_word": "happy",
              "meaning_vi": "hạnh phúc",
              "forms": {
                "comparative": "happier",
                "superlative": "happiest"
              }
            }
            """;

            var response = JsonSerializer.Deserialize<AiVocabularyAnalyzeResponse>(
                json,
                JsonOptions);

            Assert.NotNull(response);
            Assert.Equal("happy", response.BaseWord);
            Assert.NotNull(response.Forms);
            Assert.Equal("happier", response.Forms.Comparative);
            Assert.Equal("happiest", response.Forms.Superlative);
        }
    }
}
