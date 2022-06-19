module.exports = async (ip, gametype, gameport) => {
    ret = '';
    await require('gamedig').query({
        type: gametype,
        host: ip,
        port: gameport,
        maxAttempts: 5
    })
        .then(res => ret = res)
        .catch(err => ret = err);

    return ret;
}