using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MonitoramentoEnergia.Model;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace MonitoramentoEnergia.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConsumoController : ControllerBase
    {
        private readonly IMongoCollection<Consumo> consumoCollection;
        private readonly IDatabase redisDb;

        public ConsumoController()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("energy_consumption_db");
            consumoCollection = database.GetCollection<Consumo>("consumo");

            var redis = ConnectionMultiplexer.Connect("localhost:6379");
            redisDb = redis.GetDatabase();
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { message = "O serviço está rodando" });
        }

        [HttpGet("consumo")]
        public async Task<IActionResult> GetConsumo()
        {
            try
            {
                string cacheKey = "consumoData";
                string cachedData = await redisDb.StringGetAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedData))
                {
                    var cachedConsumos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Consumo>>(cachedData);
                    return Ok(new { message = "Dados recuperados do cache", data = cachedConsumos });
                }

                var consumos = await consumoCollection.Find(c => true).ToListAsync();
                if (consumos == null || consumos.Count == 0)
                {
                    return NotFound(new { message = "Nenhum dado encontrado" });
                }

                string serializedConsumos = Newtonsoft.Json.JsonConvert.SerializeObject(consumos);
                await redisDb.StringSetAsync(cacheKey, serializedConsumos, TimeSpan.FromMinutes(5));

                return Ok(new { message = "Pesquisa realizada com sucesso", data = consumos });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpPost("consumo")]
        public async Task<IActionResult> PostConsumo([FromBody] Consumo consumo)
        {
            try
            {
                await consumoCollection.InsertOneAsync(consumo);

                // Invalida o cache
                string cacheKey = "consumoData";
                await redisDb.KeyDeleteAsync(cacheKey);

                return Ok(new { message = "Cadastrado com sucesso" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}





