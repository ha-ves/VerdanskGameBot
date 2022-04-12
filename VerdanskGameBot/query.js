module.exports = async (gametype, hostport) => {
    ret = '';
    await require('gamedig').query({
        type: gametype,
        host: '192.168.30.3',
        port: hostport,
        maxAttempts: 5
    })
        .then(res => ret = res)
        .catch(err => ret = err);

    return ret;
}