using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Ai;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public partial class AiService : IAiService
    {
        public async Task<AiVocabularyResponse> AnalyzeVocabularyAsync(Guid userId, AiVocabularyRequest request)
        {
            var text = request.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Vui long nhap tu vung.");
            }

            var provider = _configuration["Ai:Provider"] ?? "Mock";
            var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
            var instruction = request.Instruction?.Trim() ?? "";
            var prompt = BuildVocabularyPrompt(text, instruction);
            string content;

            try
            {
                Task<string> aiTask;
                if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                {
                    aiTask = GenerateJsonWithGeminiAsync(prompt, geminiApiKey);
                }
                else if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                {
                    aiTask = GenerateJsonWithOpenAiAsync(prompt, openAiApiKey);
                }
                else
                {
                    return BuildVocabularyFallback(text, instruction);
                }

                var completed = await Task.WhenAny(aiTask, Task.Delay(TimeSpan.FromSeconds(18)));
                if (completed != aiTask)
                {
                    return BuildVocabularyFallback(text, instruction);
                }

                content = await aiTask;
            }
            catch
            {
                return BuildVocabularyFallback(text, instruction);
            }

            var parsed = JsonSerializer.Deserialize<AiVocabularyResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Word))
            {
                return BuildVocabularyFallback(text, instruction);
            }

            parsed.Word = string.IsNullOrWhiteSpace(parsed.Word) ? text : parsed.Word.Trim();
            parsed.Language = parsed.Language?.Trim() ?? "";
            parsed.Translation = parsed.Translation?.Trim() ?? "";
            parsed.PartOfSpeech = parsed.PartOfSpeech?.Trim() ?? "";
            parsed.Pronunciation = parsed.Pronunciation?.Trim() ?? "";
            parsed.Example = parsed.Example?.Trim() ?? "";
            parsed.ExampleTranslation = parsed.ExampleTranslation?.Trim() ?? "";
            parsed.Note = parsed.Note?.Trim() ?? "";
            parsed.Tags = parsed.Tags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList() ?? new List<string>();

            if (!parsed.Tags.Any() && !string.IsNullOrWhiteSpace(parsed.Language))
            {
                parsed.Tags.Add(parsed.Language);
            }

            return parsed;
        }

        public async Task<AiVocabularyAnalyzeResponse> AnalyzeVocabularyExtendedAsync(Guid userId, AiVocabularyAnalyzeRequest request)
        {
            var word = request.Word.Trim();
            if (request.DeckId <= 0)
            {
                throw new ArgumentException("Vui long chon bo the.");
            }

            if (string.IsNullOrWhiteSpace(word))
            {
                throw new ArgumentException("Vui long nhap tu vung.");
            }

            var deck = await _context.StudyDecks
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.DeckId && d.UserId == userId);

            if (deck == null)
            {
                throw new KeyNotFoundException("Khong tim thay bo the.");
            }

            var language = ResolveVocabularyDeckLanguage(deck.Language, deck.Name, deck.Description);
            var instruction = request.Instruction?.Trim() ?? "";
            var prompt = GetVocabularyAnalyzePrompt(language, word, instruction);
            var provider = _configuration["Ai:Provider"] ?? "Mock";
            var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
            var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

            if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
            {
                var content = await GenerateJsonWithGeminiAsync(prompt, geminiApiKey);
                return NormalizeVocabularyAnalyzeResponse(content, language, word);
            }

            if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
            {
                var content = await GenerateJsonWithOpenAiAsync(prompt, openAiApiKey);
                return NormalizeVocabularyAnalyzeResponse(content, language, word);
            }

            return BuildVocabularyAnalyzeFallback(language, word);
        }

        private static AiVocabularyAnalyzeResponse NormalizeVocabularyAnalyzeResponse(string content, string language, string word)
        {
            var parsed = JsonSerializer.Deserialize<AiVocabularyAnalyzeResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
            {
                return BuildVocabularyAnalyzeFallback(language, word);
            }

            parsed.Language = string.IsNullOrWhiteSpace(parsed.Language) ? language : ResolveVocabularyDeckLanguage(parsed.Language, null, null);
            parsed.Input = string.IsNullOrWhiteSpace(parsed.Input) ? word : parsed.Input.Trim();
            parsed.MeaningVi = parsed.MeaningVi?.Trim() ?? "";
            parsed.BaseWord = parsed.BaseWord?.Trim() ?? "";
            parsed.WordFamily ??= new AiVocabularyWordFamilyDto();
            parsed.WordFamily.Noun = CleanStringList(parsed.WordFamily.Noun);
            parsed.WordFamily.Verb = CleanStringList(parsed.WordFamily.Verb);
            parsed.WordFamily.Adjective = CleanStringList(parsed.WordFamily.Adjective);
            parsed.WordFamily.Adverb = CleanStringList(parsed.WordFamily.Adverb);
            parsed.Synonyms = parsed.Synonyms?
                .Where(item => item != null && (!string.IsNullOrWhiteSpace(item.Word) || !string.IsNullOrWhiteSpace(item.MeaningVi)))
                .Select(item => new AiVocabularySynonymDto
                {
                    Word = item.Word?.Trim() ?? "",
                    Reading = item.Reading?.Trim() ?? "",
                    MeaningVi = item.MeaningVi?.Trim() ?? "",
                    Note = item.Note?.Trim() ?? ""
                })
                .Take(20)
                .ToList() ?? new List<AiVocabularySynonymDto>();

            return parsed;
        }

        private static List<string> CleanStringList(List<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList() ?? new List<string>();
        }

        private static AiVocabularyAnalyzeResponse BuildVocabularyAnalyzeFallback(string language, string word)
        {
            var normalized = ResolveVocabularyDeckLanguage(language, null, null);
            var trimmed = word.Trim();
            var lower = trimmed.ToLowerInvariant();

            if (normalized == "japanese" && (lower == "です" || lower == "desu"))
            {
                return new AiVocabularyAnalyzeResponse
                {
                    Language = "japanese",
                    Input = "です (desu)",
                    MeaningVi = "là; thì; trợ động từ lịch sự dùng cuối câu",
                    BaseWord = "",
                    WordFamily = new AiVocabularyWordFamilyDto(),
                    Synonyms = new List<AiVocabularySynonymDto>
                    {
                        new()
                        {
                            Word = "だ (da)",
                            Reading = "da",
                            MeaningVi = "là; thì",
                            Note = "Dạng ngắn gọn, thân mật hơn です."
                        },
                        new()
                        {
                            Word = "であります (de arimasu)",
                            Reading = "de arimasu",
                            MeaningVi = "là; thì",
                            Note = "Trang trọng hơn, thường dùng trong văn bản hoặc phát biểu."
                        }
                    }
                };
            }

            if (normalized == "japanese" && (lower == "わたし" || lower == "私" || lower == "watashi"))
            {
                return new AiVocabularyAnalyzeResponse
                {
                    Language = "japanese",
                    Input = "わたし (watashi)",
                    MeaningVi = "tôi",
                    BaseWord = "",
                    WordFamily = new AiVocabularyWordFamilyDto(),
                    Synonyms = new List<AiVocabularySynonymDto>
                    {
                        new()
                        {
                            Word = "ぼく (boku)",
                            Reading = "boku",
                            MeaningVi = "tôi",
                            Note = "Thường dùng bởi nam giới, thân mật hơn."
                        },
                        new()
                        {
                            Word = "あたし (atashi)",
                            Reading = "atashi",
                            MeaningVi = "tôi",
                            Note = "Thân mật, thường mang sắc thái nữ tính."
                        }
                    }
                };
            }

            return new AiVocabularyAnalyzeResponse
            {
                Language = normalized,
                Input = trimmed,
                MeaningVi = "",
                BaseWord = "",
                WordFamily = new AiVocabularyWordFamilyDto(),
                Synonyms = new List<AiVocabularySynonymDto>()
            };
        }

        private static string ResolveVocabularyDeckLanguage(string? language, string? deckName, string? description)
        {
            var value = language?.Trim().ToLowerInvariant() ?? "";
            var normalized = value switch
            {
                "english" or "en" or "eng" or "tieng anh" or "tiếng anh" => "english",
                "japanese" or "ja" or "jp" or "japan" or "nihongo" or "tieng nhat" or "tiếng nhật" => "japanese",
                _ => value
            };

            if (!string.IsNullOrWhiteSpace(normalized) && normalized != "other")
            {
                return normalized;
            }

            var hint = $"{deckName} {description}".ToLowerInvariant();
            if (hint.Contains("english") || hint.Contains("tieng anh") || hint.Contains("tiếng anh") || hint.Contains("interview"))
            {
                return "english";
            }

            if (hint.Contains("japanese") || hint.Contains("japan") || hint.Contains("n5") || hint.Contains("nihongo") || hint.Contains("tieng nhat") || hint.Contains("tiếng nhật"))
            {
                return "japanese";
            }

            return string.IsNullOrWhiteSpace(normalized) ? "other" : normalized;
        }

        private static AiVocabularyResponse BuildVocabularyFallback(string text, string instruction = "")
        {
            var normalized = text.Trim();
            var lower = normalized.ToLowerInvariant();
            var wantsKana = true;
            var hasJapanese = normalized.Any(ch =>
                (ch >= '\u3040' && ch <= '\u30ff') ||
                (ch >= '\u4e00' && ch <= '\u9fff'));
            var isAscii = normalized.All(ch => ch <= 127);

            var response = new AiVocabularyResponse
            {
                Word = normalized,
                Language = hasJapanese ? "Tiếng Nhật" : isAscii ? "Tiếng Anh" : "Ngôn ngữ khác",
                Translation = normalized,
                PartOfSpeech = "",
                Pronunciation = "",
                Example = "",
                ExampleTranslation = "",
                Note = "AI chưa phản hồi kịp. Bạn có thể sửa lại thông tin trước khi lưu.",
                Tags = new List<string>()
            };

            if (lower == "わたし" || lower == "私" || lower == "watashi")
            {
                response.Word = "わたし (watashi)";
                response.Language = "Tiếng Nhật";
                response.Translation = "tôi";
                response.PartOfSpeech = "đại từ";
                response.Pronunciation = wantsKana ? "romaji: watashi" : "watashi";
                response.Example = "わたしは学生です。 (Watashi wa gakusei desu.)";
                response.ExampleTranslation = "Tôi là học sinh/sinh viên.";
                response.Note = "Cách nói trung tính, lịch sự để tự xưng là tôi.";
                response.Tags = new List<string> { "Tiếng Nhật", "N5", "Pronoun" };
                return response;
            }

            if (lower == "です" || lower == "desu")
            {
                response.Word = "です (desu)";
                response.Language = "Tiếng Nhật";
                response.Translation = "là; thì; trợ động từ lịch sự dùng cuối câu";
                response.PartOfSpeech = "trợ động từ";
                response.Pronunciation = "romaji: desu";
                response.Example = "私は学生です。 (Watashi wa gakusei desu.)";
                response.ExampleTranslation = "Tôi là học sinh/sinh viên.";
                response.Note = "Dùng ở cuối câu để tạo cách nói lịch sự.";
                response.Tags = new List<string> { "Tiếng Nhật", "N5", "Grammar" };
                return response;
            }

            if (hasJapanese)
            {
                response.Tags.AddRange(new[] { "Tiếng Nhật", "Vocabulary" });
            }
            else if (isAscii)
            {
                response.Tags.AddRange(new[] { "English", "Vocabulary" });
            }
            else
            {
                response.Tags.Add("Vocabulary");
            }

            return response;
        }

        private static string GetVocabularyAnalyzePrompt(string language, string word, string instruction = "")
        {
            var normalized = ResolveVocabularyDeckLanguage(language, null, null);

            return normalized switch
            {
                "english" => BuildEnglishVocabularyAnalyzePrompt(word, instruction),
                "japanese" => BuildJapaneseVocabularyAnalyzePrompt(word, instruction),
                _ => throw new NotSupportedException("Admin chưa thêm xử lý AI cho ngôn ngữ này.")
            };
        }

        private static string BuildEnglishVocabularyAnalyzePrompt(string word, string instruction = "")
        {
            var input = JsonSerializer.Serialize(word);
            var instructionBlock = string.IsNullOrWhiteSpace(instruction)
                ? ""
                : $"\nIMPORTANT USER INSTRUCTION / NOTE:\n{instruction.Trim()}\n(You MUST strictly adhere to this user instruction while performing the analysis.)\n";

            return $$"""
You are an English vocabulary analysis assistant.

Analyze the given English word and return ONLY valid JSON.

Input word: {{input}}
{{instructionBlock}}
Requirements & Rules:
1. Base/Root Word ("base_word"):
   - CRITICAL LEMMATIZATION: Whenever the input is an inflected form (e.g. stopped -> stop, running -> run, played -> play, happier -> happy, better -> good, went -> go) or a clear derivational form with a direct common root (e.g. decision -> decide, creative -> create, teacher -> teach), ALWAYS return the dictionary base/root/lemma form (nguyên mẫu).
   - CRITICAL RULE FOR COMPOUND WORDS & STANDALONE DISTINCT WORDS:
     Do NOT decompose compound words, portmanteaus, idioms, or distinct standalone words into their component parts.
     For example:
     - "foolproof" is a distinct compound adjective meaning incapable of going wrong. It is NOT "fool" or "proof". Its base_word MUST be "foolproof".
     - "deadline" -> base_word is "deadline" (NOT "dead").
     - "butterfly" -> base_word is "butterfly" (NOT "butter").
     - "waterproof" -> base_word is "waterproof" (NOT "water").
     - "sunflower" -> base_word is "sunflower" (NOT "sun").
     - "software" -> base_word is "software" (NOT "soft").
     - "breakthrough" -> base_word is "breakthrough" (NOT "break").
     - "blackboard" -> base_word is "blackboard" (NOT "black").
     - "understand" -> base_word is "understand" (NOT "stand").
     If the input word is already a root word, a compound word, or has an independent meaning, the "base_word" field MUST BE the input word itself.
2. Word Family ("word_family"):
   - Return the natural word family members focusing strictly on Noun (danh từ), Verb (động từ), and Adjective (tính từ) that genuinely share the same root in English.
   - Example for "stop": noun: ["stop", "stoppage"], verb: ["stop"], adjective: ["stoppable", "unstoppable"].
   - Example for "decide": noun: ["decision"], verb: ["decide"], adjective: ["decisive"].
   - Do NOT fabricate or invent unnatural words. If a part of speech does not naturally exist in the root family (e.g. "foolproof" has no separate verb or noun in that exact family), leave its array empty [].
3. Vietnamese Meaning ("meaning_vi"):
   - Provide an accurate, concise Vietnamese translation with full diacritics.
4. Synonyms ("synonyms"):
   - Return natural synonyms with concise Vietnamese meaning ("meaning_vi" with full diacritics) and short nuance note ("note").
5. Return JSON ONLY without any markdown code fences, headers, or explanations outside the JSON.

JSON format:
{
  "language": "english",
  "input": "",
  "base_word": "",
  "meaning_vi": "",
  "word_family": {
    "noun": [],
    "verb": [],
    "adjective": [],
    "adverb": []
  },
  "synonyms": [
    {
      "word": "",
      "meaning_vi": "",
      "note": ""
    }
  ]
}
""";
        }

        private static string BuildJapaneseVocabularyAnalyzePrompt(string word, string instruction = "")
        {
            var input = JsonSerializer.Serialize(word);
            var instructionBlock = string.IsNullOrWhiteSpace(instruction)
                ? ""
                : $"\nIMPORTANT USER INSTRUCTION / NOTE:\n{instruction.Trim()}\n(You MUST strictly adhere to this user instruction while performing the analysis.)\n";

            return $$"""
You are a Japanese vocabulary assistant.

Analyze the given Japanese word and return ONLY valid JSON.

Input word: {{input}}
{{instructionBlock}}
Requirements:
1. Format rules:
   - If a Japanese word has kanji, write the "word" field as Kanji with kana beside it, for example "火災（かさい）" or "火事（かじ）".
   - Put romaji in the "reading" field, for example "kasai" or "kaji".
2. Synonyms:
   - Return synonyms or near-synonyms with concise Vietnamese meanings and usage notes.
   - If there are no good synonyms, return an empty array [].
3. Vietnamese:
   - All Vietnamese translations and notes must use full Vietnamese diacritics.
4. Return JSON ONLY without any markdown wrapping or explanation outside JSON.

JSON format:
{
  "language": "japanese",
  "input": "",
  "meaning_vi": "",
  "synonyms": [
    {
      "word": "Japanese word (reading)",
      "reading": "romaji",
      "meaning_vi": "",
      "note": ""
    }
  ]
}
""";
        }

        private string BuildVocabularyPrompt(string text, string instruction = "")
        {
            var input = JsonSerializer.Serialize(text);
            var instructionSection = string.IsNullOrWhiteSpace(instruction)
                ? ""
                : $"\nIMPORTANT USER NOTE / INSTRUCTION:\n{instruction.Trim()}\n(You MUST strictly follow and apply the user's note/instruction above when generating the flashcard content.)\n";

            return $$"""
You are a vocabulary flashcard assistant for Vietnamese learners.
Analyze the input word or short phrase in any language (English, Japanese, Korean, Chinese, French, German, Spanish, etc.).
Return ONLY valid JSON with exactly these fields:
{
  "word": "the exact word or phrase being studied",
  "language": "detected language in Vietnamese, e.g. Tiếng Anh, Tiếng Nhật",
  "translation": "Vietnamese meaning with full diacritics",
  "partOfSpeech": "part of speech in Vietnamese (e.g. danh từ, động từ, tính từ, trạng từ); empty if unknown",
  "pronunciation": "IPA pronunciation for English, romaji/kana for Japanese, pinyin for Chinese, etc.",
  "example": "one natural example sentence in the source language",
  "exampleTranslation": "Vietnamese translation of the example with full diacritics",
  "note": "short Vietnamese usage note",
  "tags": ["language tag", "topic or level tag"]
}

Core Rules:
1. All Vietnamese explanations, translations, notes, and examples MUST use proper Vietnamese with full diacritics (dấu tiếng Việt).
2. For English:
   - CRITICAL LEMMATIZATION RULE FOR "word": Whenever the input word is an inflected form (e.g. "stopped" -> "stop", "running" -> "run", "played" -> "play", "happier" -> "happy", "studies" or "studied" -> "study", "better" -> "good", "swimming" -> "swim", "went" -> "go"), the "word" field MUST ALWAYS be converted to its dictionary base/root/lemma form (nguyên mẫu). Do NOT keep past tense (-ed), progressive/gerund (-ing), plural (-s/-es), or comparative/superlative inflections in the "word" field.
   - However, do NOT decompose genuine compound words, idioms, or distinct standalone words (e.g. "foolproof" stays "foolproof", "deadline" stays "deadline", "butterfly" stays "butterfly", "software" stays "software").
   - Include accurate IPA pronunciation of the base word (e.g. /ˈfuːl.pruːf/, /stɒp/).
3. For Japanese:
   - For Kanji words, include furigana/kana in parentheses if helpful, e.g., "火災（かさい）".
   - Include romaji reading in pronunciation or next to Japanese text.
4. Do not include markdown code block backticks outside the JSON or conversational text. Return raw JSON only.
{{instructionSection}}
Input: {{input}}
""";
        }
    }
}
