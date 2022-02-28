const { auth } = require('express-openid-connect');
const UserManager = require('../helpers/UserManager');
const Utils = require('../helpers/utils');
const Settings = require('../models/Settings');
const System = require('../models/System');

class OAuthStrategy {

    async saveInitialConfig(settings){
        let system = System.getInstance();
        system.setProperty('OAuth', {            
            IssuerBaseUrl: settings.IssuerBaseUrl,
            ClientId: settings.ClientId,
            BaseUrl: settings.BaseUrl,
            Secret: settings.Secret
        });
        let adminUser = settings.AdminUsername;
        let userManager = UserManager.getInstance();
        await userManager.register(adminUser, new Utils().newGuid(), true);
        return true;
    }

    init(app) 
    {
        let system = System.getInstance();
        let oauth = system.getProperty('OAuth');
        app.use(
            auth({
                authRequired: false,
                issuerBaseURL: oauth.IssuerBaseUrl,
                clientID: oauth.ClientId,
                baseURL: oauth.BaseUrl,
                secret: oauth.Secret
            })
        );

        app.use(async (req, res, next) =>{
            console.log('oauth middleware!!!');
            if(req.oidc.isAuthenticated()){
                if(!req.user){
                    console.log('user: ', req.oidc.user);
                    let userManager = UserManager.getInstance();
                    let username = req.oidc.user.email || req.oidc.user.username || req.oidc.user.name;
                    req.user = userManager.getUser(username);
                    if(!req.user){
                        // register the user
                        req.user = await userManager.register(username, new Utils().newGuid());
                    }
                    req.settings = await Settings.getForUser(req.user.Uid);
                }
            }
            else 
            {                
                req.isGuest = true;
                req.settings = await Settings.getForGuest();
            }
            next();
        })

        //app.post('/callback');
    }
}

module.exports = OAuthStrategy;