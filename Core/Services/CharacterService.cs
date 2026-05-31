using Core.Interfaces;
using Core.Models.Entities;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Core.Services
{
    public class CharacterService
    {
        private readonly IGameDataRepository _gameDataRepository;

        public CharacterService(IGameDataRepository gameDataRepository)
        {
            _gameDataRepository = gameDataRepository;
        }

        public async Task<List<Character>> GetAllAsync()
        {
            var charactersData = await _gameDataRepository.GetCharactersAsync();
            
            if (charactersData == null)
            {
                throw new Exception("Failed to retrieve character data.");
            }

            return charactersData.Values.ToList();
        }
    }
}   
