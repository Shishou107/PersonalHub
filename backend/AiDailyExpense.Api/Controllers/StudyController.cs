using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Study;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudyController : ControllerBase
    {
        private readonly IStudyService _studyService;
        private readonly CurrentUserHelper _currentUserHelper;

        public StudyController(IStudyService studyService, CurrentUserHelper currentUserHelper)
        {
            _studyService = studyService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet("decks")]
        public async Task<IActionResult> GetDecks()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var decks = await _studyService.GetDecksAsync(userId);
                return Ok(ApiResponse<List<StudyDeckDto>>.Ok(decks, "Lay danh sach bo the thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<StudyDeckDto>>.Fail(ex.Message));
            }
        }

        [HttpPost("decks")]
        public async Task<IActionResult> CreateDeck([FromBody] CreateStudyDeckRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var deck = await _studyService.CreateDeckAsync(userId, request);
                return Ok(ApiResponse<StudyDeckDto>.Ok(deck, "Tao bo the thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<StudyDeckDto>.Fail(ex.Message));
            }
        }

        [HttpDelete("decks/{id}")]
        public async Task<IActionResult> DeleteDeck(int id)
        {
            return await DeleteDeckInternal(id);
        }

        [HttpPost("decks/{id}/delete")]
        public async Task<IActionResult> DeleteDeckByPost(int id)
        {
            return await DeleteDeckInternal(id);
        }

        private async Task<IActionResult> DeleteDeckInternal(int id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _studyService.DeleteDeckAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Khong tim thay bo the."));
                }

                return Ok(ApiResponse<bool>.Ok(true, "Xoa bo the thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpGet("cards")]
        public async Task<IActionResult> GetCards([FromQuery] int? deckId)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var cards = await _studyService.GetCardsAsync(userId, deckId);
                return Ok(ApiResponse<List<StudyCardDto>>.Ok(cards, "Lay danh sach the thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<StudyCardDto>>.Fail(ex.Message));
            }
        }

        [HttpPost("cards")]
        public async Task<IActionResult> CreateCard([FromBody] CreateStudyCardRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var card = await _studyService.CreateCardAsync(userId, request);
                return Ok(ApiResponse<StudyCardDto>.Ok(card, "Tao the hoc thanh cong"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<StudyCardDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<StudyCardDto>.Fail(ex.Message));
            }
        }

        [HttpDelete("cards/{id}")]
        public async Task<IActionResult> DeleteCard(long id)
        {
            return await DeleteCardInternal(id);
        }

        [HttpPost("cards/{id}/delete")]
        public async Task<IActionResult> DeleteCardByPost(long id)
        {
            return await DeleteCardInternal(id);
        }

        private async Task<IActionResult> DeleteCardInternal(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _studyService.DeleteCardAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Khong tim thay the hoc."));
                }

                return Ok(ApiResponse<bool>.Ok(true, "Xoa the hoc thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetDueCards([FromQuery] int? deckId)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var cards = await _studyService.GetDueCardsAsync(userId, deckId);
                return Ok(ApiResponse<List<StudyCardDto>>.Ok(cards, "Lay the can on thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<StudyCardDto>>.Fail(ex.Message));
            }
        }

        [HttpPost("cards/{id}/review")]
        public async Task<IActionResult> ReviewCard(long id, [FromBody] ReviewStudyCardRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _studyService.ReviewCardAsync(userId, id, request);
                return Ok(ApiResponse<StudyReviewResultDto>.Ok(result, "Luu ket qua on tap thanh cong"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<StudyReviewResultDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<StudyReviewResultDto>.Fail(ex.Message));
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var stats = await _studyService.GetStatsAsync(userId);
                return Ok(ApiResponse<StudyStatsDto>.Ok(stats, "Lay thong ke hoc bai thanh cong"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<StudyStatsDto>.Fail(ex.Message));
            }
        }
    }
}
