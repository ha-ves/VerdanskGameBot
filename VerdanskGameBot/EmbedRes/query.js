const { GameDig } = require('gamedig');

/**
 * Queries a game server using gamedig.
 * @param {string} ip - The IP address of the server.
 * @param {string} gametype - The type of the game.
 * @param {number} gameport - The port of the game server.
 * @param {number} attempts - Number of retry attempts.
 * @param {number} timeoutMs - Timeout in milliseconds for each attempt.
 * @returns {Promise<Object>} The result of the query.
 */
module.exports = async function queryServer(ip, gametype, gameport, attempts, timeoutMs) {
    // Let errors reject so .NET gets a proper exception
    return await GameDig.query({
        type: gametype,
        host: ip,
        ...(gameport !== 0 && { port: gameport }),
        maxRetries: attempts,
        attemptTimeout: timeoutMs // verify against your gamedig version; some use `socketTimeout`
    });
};