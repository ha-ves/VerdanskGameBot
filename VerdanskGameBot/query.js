module.exports = async (ip, gametype, gameport, attempts, timeoutMs) => {
    ret = '';
    await require('gamedig').query({
        type: gametype,
        host: ip,
        port: gameport,
        maxAttempts: attempts,
        attemptTimeout: timeoutMs
    })
        .then(res => ret = res)
        .catch(err => ret = err);

    return ret;
}