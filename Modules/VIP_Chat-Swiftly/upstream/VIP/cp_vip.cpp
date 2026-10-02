#include <stdio.h>
#include "cp_vip.h"
#include "metamod_oslink.h"
#include "schemasystem/schemasystem.h"

CP_VIP g_CP_VIP;
PLUGIN_EXPOSE(CP_VIP, g_CP_VIP);
IVEngineServer2* engine = nullptr;
CGameEntitySystem* g_pGameEntitySystem = nullptr;
CEntitySystem* g_pEntitySystem = nullptr;
CGlobalVars *gpGlobals = nullptr;

IVIPApi* g_pVIPCore;
IUtilsApi* g_pUtils;
IMenusApi* g_pMenus;
IChatProcessorApi* g_pCP;

bool g_bChatInput[64];

struct VIPData
{
	std::string szPrefix;
	std::string szPrefixSet;
	std::string szNameColor;
	std::string szNameColorSet;
	std::string szTextColor;
	std::string szTextColorSet;
	std::string szPrefixColor;
	std::string szPrefixColorSet;
};

VIPData g_VIPData[64];

std::map<std::string, std::string> g_mColors;
std::map<std::string, std::string> g_mPrefixes;

CGameEntitySystem* GameEntitySystem()
{
	return g_pUtils->GetCGameEntitySystem();
}

void StartupServer()
{
	g_pGameEntitySystem = GameEntitySystem();
	g_pEntitySystem = g_pUtils->GetCEntitySystem();
	gpGlobals = g_pUtils->GetCGlobalVars();
}

bool CP_VIP::Load(PluginId id, ISmmAPI* ismm, char* error, size_t maxlen, bool late)
{
	PLUGIN_SAVEVARS();

	GET_V_IFACE_CURRENT(GetEngineFactory, g_pCVar, ICvar, CVAR_INTERFACE_VERSION);
	GET_V_IFACE_ANY(GetEngineFactory, g_pSchemaSystem, ISchemaSystem, SCHEMASYSTEM_INTERFACE_VERSION);
	GET_V_IFACE_CURRENT(GetFileSystemFactory, g_pFullFileSystem, IFileSystem, FILESYSTEM_INTERFACE_VERSION);
	GET_V_IFACE_CURRENT(GetEngineFactory, engine, IVEngineServer2, SOURCE2ENGINETOSERVER_INTERFACE_VERSION);

	g_SMAPI->AddListener( this, this );

	return true;
}

bool CP_VIP::Unload(char *error, size_t maxlen)
{
	ConVar_Unregister();
	
	return true;
}

bool OnChatMesssage(int iSlot, std::string &szName, std::string &szMessage)
{
	szName = g_VIPData[iSlot].szPrefixColorSet + g_VIPData[iSlot].szPrefixSet + g_VIPData[iSlot].szNameColorSet + szName;
	szMessage = g_VIPData[iSlot].szTextColorSet + szMessage;
	return true;
}

void VIP_OnClientLoaded(int iSlot, bool bIsVIP)
{
    if(bIsVIP)
    {	
        std::string szPrefix = g_pVIPCore->VIP_GetClientFeatureString(iSlot, "Chat_Prefix");
        if(szPrefix.find("custom") != std::string::npos || szPrefix.find("list") != std::string::npos)
        {
            std::string szPrefixSet = g_pVIPCore->VIP_GetClientCookie(iSlot, "Chat_PrefixValue");
            g_VIPData[iSlot].szPrefixSet = szPrefixSet;
        } else {
            g_VIPData[iSlot].szPrefixSet = szPrefix;
        }
        
        std::string szNameColor = g_pVIPCore->VIP_GetClientFeatureString(iSlot, "Chat_NameColor");
        if(szNameColor.find("list") != std::string::npos)
        {
            std::string szNameColorSet = g_pVIPCore->VIP_GetClientCookie(iSlot, "Chat_NameColorValue");
            g_VIPData[iSlot].szNameColorSet = szNameColorSet;
        } else {
            g_VIPData[iSlot].szNameColorSet = szNameColor;
        }

        std::string szTextColor = g_pVIPCore->VIP_GetClientFeatureString(iSlot, "Chat_TextColor");
        if(szTextColor.find("list") != std::string::npos)
        {
            std::string szTextColorSet = g_pVIPCore->VIP_GetClientCookie(iSlot, "Chat_TextColorValue");
            g_VIPData[iSlot].szTextColorSet = szTextColorSet;
        } else {
            g_VIPData[iSlot].szTextColorSet = szTextColor;
        }

        std::string szPrefixColor = g_pVIPCore->VIP_GetClientFeatureString(iSlot, "Chat_PrefixColor");
        if(szPrefixColor.find("list") != std::string::npos)
        {
            std::string szPrefixColorSet = g_pVIPCore->VIP_GetClientCookie(iSlot, "Chat_PrefixColorValue");
            g_VIPData[iSlot].szPrefixColorSet = szPrefixColorSet;
        } else {
            g_VIPData[iSlot].szPrefixColorSet = szPrefixColor;
        }

        g_VIPData[iSlot].szPrefix = szPrefix;
        g_VIPData[iSlot].szNameColor = szNameColor;
        g_VIPData[iSlot].szTextColor = szTextColor;
        g_VIPData[iSlot].szPrefixColor = szPrefixColor;
    } else {
		g_VIPData[iSlot].szPrefix.clear();
		g_VIPData[iSlot].szPrefixSet.clear();
		g_VIPData[iSlot].szNameColor.clear();
		g_VIPData[iSlot].szNameColorSet.clear();
		g_VIPData[iSlot].szTextColor.clear();
		g_VIPData[iSlot].szTextColorSet.clear();
		g_VIPData[iSlot].szPrefixColor.clear();
		g_VIPData[iSlot].szPrefixColorSet.clear();
	}
}

bool OnSelect(int iSlot, const char* szFeature);

void ShowColorList(int iSlot, int iBack)
{
	Menu hMenu;
	g_pMenus->SetTitleMenu(hMenu, g_pVIPCore->VIP_GetTranslate("Chat_TitleColor"));
	g_pMenus->AddItemMenu(hMenu, "", g_pVIPCore->VIP_GetTranslate("Chat_Disable"));
	for (const auto& element : g_mColors) {
		g_pMenus->AddItemMenu(hMenu, element.second.c_str(), element.first.c_str());
	}
	g_pMenus->SetExitMenu(hMenu, true);
	g_pMenus->SetBackMenu(hMenu, true);
	g_pMenus->SetCallback(hMenu, [iBack](const char* szBack, const char* szFront, int iItem, int iSlot) {
		if(iItem < 7)
		{
			switch(iBack)
			{
				case 2:
					g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_NameColorValue", szBack);
					g_VIPData[iSlot].szNameColorSet = szBack;
					break;
				case 3:
					g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_TextColorValue", szBack);
					g_VIPData[iSlot].szTextColorSet = szBack;
					break;
				case 4:
					g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixColorValue", szBack);
					g_VIPData[iSlot].szPrefixColorSet = szBack;
					break;
			}
			OnSelect(iSlot, "Chat");
		} else if(iItem == 7) OnSelect(iSlot, "Chat");
	});
	g_pMenus->DisplayPlayerMenu(hMenu, iSlot);
}

void ShowPrefixList(int iSlot)
{
	Menu hMenu;
	g_pMenus->SetTitleMenu(hMenu, g_pVIPCore->VIP_GetTranslate("Chat_TitlePrefix"));
	g_pMenus->AddItemMenu(hMenu, "", g_pVIPCore->VIP_GetTranslate("Chat_Disable"));
	for (const auto& element : g_mPrefixes) {
		g_pMenus->AddItemMenu(hMenu, element.second.c_str(), element.first.c_str());
	}
	g_pMenus->SetExitMenu(hMenu, true);
	g_pMenus->SetBackMenu(hMenu, true);
	g_pMenus->SetCallback(hMenu, [](const char* szBack, const char* szFront, int iItem, int iSlot) {
		if(iItem < 7)
		{
			g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixValue", szBack);
			g_VIPData[iSlot].szPrefixSet = szBack;
			OnSelect(iSlot, "Chat");
		} else if(iItem == 7) OnSelect(iSlot, "Chat");
	});
	g_pMenus->DisplayPlayerMenu(hMenu, iSlot);
}

bool OnSelect(int iSlot, const char* szFeature)
{
	Menu hMenu;
	g_pMenus->SetTitleMenu(hMenu, g_pVIPCore->VIP_GetTranslate("Chat_Title"));
	char szBuffer[128];
	g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_Prefix"), g_VIPData[iSlot].szPrefixSet.size() ? g_VIPData[iSlot].szPrefixSet.c_str() : g_pVIPCore->VIP_GetTranslate("Chat_Default"));
	g_pMenus->AddItemMenu(hMenu, "1", szBuffer);
	if(g_VIPData[iSlot].szNameColorSet.size()) {
		//Find color name
		std::string szNameColor = g_VIPData[iSlot].szNameColorSet;
		std::string szNameColorName;
		for (const auto& element : g_mColors) {
			if(element.second == szNameColor) {
				szNameColorName = element.first;
				break;
			}
		}
		if(szNameColorName.size()) {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_NameColor"), szNameColorName.c_str());
		} else {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_NameColor"), g_VIPData[iSlot].szNameColorSet.c_str());
		}
	} else {
		g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_NameColor"), g_pVIPCore->VIP_GetTranslate("Chat_Default"));
	}
	g_pMenus->AddItemMenu(hMenu, "2", szBuffer);
	if(g_VIPData[iSlot].szTextColorSet.size()) {
		std::string szTextColor = g_VIPData[iSlot].szTextColorSet;
		std::string szTextColorName;
		for (const auto& element : g_mColors) {
			if(element.second == szTextColor) {
				szTextColorName = element.first;
				break;
			}
		}
		if(szTextColorName.size()) {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_TextColor"), szTextColorName.c_str());
		} else {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_TextColor"), g_VIPData[iSlot].szTextColorSet.c_str());
		}
	} else {
		g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_TextColor"), g_pVIPCore->VIP_GetTranslate("Chat_Default"));
	}
	g_pMenus->AddItemMenu(hMenu, "3", szBuffer);
	if(g_VIPData[iSlot].szPrefixColorSet.size()) {
		std::string szPrefixColor = g_VIPData[iSlot].szPrefixColorSet;
		std::string szPrefixColorName;
		for (const auto& element : g_mColors) {
			if(element.second == szPrefixColor) {
				szPrefixColorName = element.first;
				break;
			}
		}
		if(szPrefixColorName.size()) {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_PrefixColor"), szPrefixColorName.c_str());
		} else {
			g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_PrefixColor"), g_VIPData[iSlot].szPrefixColorSet.c_str());
		}
	} else {
		g_SMAPI->Format(szBuffer, sizeof(szBuffer), g_pVIPCore->VIP_GetTranslate("Chat_PrefixColor"), g_pVIPCore->VIP_GetTranslate("Chat_Default"));
	}
	g_pMenus->AddItemMenu(hMenu, "4", szBuffer);

	g_pMenus->SetExitMenu(hMenu, true);
	g_pMenus->SetBackMenu(hMenu, true);

	g_pMenus->SetCallback(hMenu, [](const char* szBack, const char* szFront, int iItem, int iSlot)
	{
		if(iItem < 7)
		{
			int iBack = atoi(szBack);
			switch (iBack)
			{
				case 1:
					if(g_VIPData[iSlot].szPrefix.find("custom") != std::string::npos) {
						if(g_VIPData[iSlot].szPrefixSet.size() > 0)
						{
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixValue", "");
							g_VIPData[iSlot].szPrefixSet.clear();
							OnSelect(iSlot, "Chat");
						} else {
							g_bChatInput[iSlot] = true;
							g_pUtils->PrintToChat(iSlot, g_pVIPCore->VIP_GetTranslate("CP_PrefixInput"));
						}
					} else if(g_VIPData[iSlot].szPrefix.find("list") != std::string::npos) {
						ShowPrefixList(iSlot);
					} else {
						if(g_VIPData[iSlot].szPrefixSet.size() > 0)
						{
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixValue", "");
							g_VIPData[iSlot].szPrefixSet.clear();
						} else {
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixValue", g_VIPData[iSlot].szPrefix.c_str());
							g_VIPData[iSlot].szPrefixSet = g_VIPData[iSlot].szPrefix;
						}
						OnSelect(iSlot, "Chat");
					}
					break;
				case 2:
					if(g_VIPData[iSlot].szNameColor.find("list") != std::string::npos) {
						ShowColorList(iSlot, iBack);
					} else {
						if(g_VIPData[iSlot].szNameColorSet.size() > 0)
						{
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_NameColorValue", "");
							g_VIPData[iSlot].szNameColorSet.clear();
						} else {
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_NameColorValue", g_VIPData[iSlot].szNameColor.c_str());
							g_VIPData[iSlot].szNameColorSet = g_VIPData[iSlot].szNameColor;
						}
						OnSelect(iSlot, "Chat");
					}
					break;
				case 3:
					if(g_VIPData[iSlot].szTextColor.find("list") != std::string::npos) {
						ShowColorList(iSlot, iBack);
					} else {
						if(g_VIPData[iSlot].szTextColorSet.size() > 0)
						{
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_TextColorValue", "");
							g_VIPData[iSlot].szTextColorSet.clear();
						} else {
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_TextColorValue", g_VIPData[iSlot].szTextColor.c_str());
							g_VIPData[iSlot].szTextColorSet = g_VIPData[iSlot].szTextColor;
						}
						OnSelect(iSlot, "Chat");
					}
					break;
				case 4:
					if(g_VIPData[iSlot].szPrefixColor.find("list") != std::string::npos) {
						ShowColorList(iSlot, iBack);
					} else {
						if(g_VIPData[iSlot].szPrefixColorSet.size() > 0)
						{
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixColorValue", "");
							g_VIPData[iSlot].szPrefixColorSet.clear();
						} else {
							g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixColorValue", g_VIPData[iSlot].szPrefixColor.c_str());
							g_VIPData[iSlot].szPrefixColorSet = g_VIPData[iSlot].szPrefixColor;
						}
						OnSelect(iSlot, "Chat");
					}
					break;
			}
		}
		else if(iItem == 7) g_pVIPCore->VIP_OpenMenu(iSlot);
	});

	g_pMenus->DisplayPlayerMenu(hMenu, iSlot);
	return false;
}

std::string EscapeString(const std::string &str)
{
	std::string result;
	for (size_t i = 0; i < str.size(); i++)
	{
		if (str[i] == '"') continue;
		if (str[i] == '{' || str[i] == '}' || str[i] == '\\')
			result += '\\';
		result += str[i];
	}
	return result;
}

bool OnChatPre(int iSlot, const char* szContent, bool bTeam)
{
	if(g_bChatInput[iSlot])
	{
		//check szContent contains !cancel
		if(strstr(szContent, "!cancel"))
		{
			g_bChatInput[iSlot] = false;
			g_pUtils->PrintToChat(iSlot, g_pVIPCore->VIP_GetTranslate("CP_PrefixInputCancel"));
			return false;
		}
		g_bChatInput[iSlot] = false;
		//Убираем первый символ и последний символ
		std::string szContentStr = szContent;
		szContentStr = szContentStr.substr(1, szContentStr.size() - 2);
		std::string szPrefix = EscapeString(szContentStr);
		g_pVIPCore->VIP_SetClientCookie(iSlot, "Chat_PrefixValue", szPrefix.c_str());
		g_VIPData[iSlot].szPrefixSet = szPrefix.c_str();
		g_pUtils->PrintToChat(iSlot, g_pVIPCore->VIP_GetTranslate("CP_PrefixInputSuccess"));
		
		OnSelect(iSlot, "Chat");

		return false;
	}
	return true;
}

void LoadConfig()
{
	g_mColors.clear();
	g_mPrefixes.clear();
	KeyValues* g_kvSettings = new KeyValues("Chat");
	const char *pszPath = "addons/configs/vip/chat.ini";

	if (!g_kvSettings->LoadFromFile(g_pFullFileSystem, pszPath))
	{
		Warning("Failed to load %s\n", pszPath);
		return;
	}

	KeyValues* pColor = g_kvSettings->FindKey("Color_List");
	if(pColor)
	{
		FOR_EACH_VALUE(pColor, pValue)
		{
			g_mColors[pValue->GetName()] = pValue->GetString(nullptr, nullptr);
		}
	}
	KeyValues* pPrefix = g_kvSettings->FindKey("Prefix_List");
	if(pPrefix)
	{
		FOR_EACH_VALUE(pPrefix, pValue)
		{
			g_mPrefixes[pValue->GetName()] = pValue->GetString(nullptr, nullptr);
		}
	}
}

void CP_VIP::AllPluginsLoaded()
{
	char error[64];
	int ret;
	g_pUtils = (IUtilsApi *)g_SMAPI->MetaFactory(Utils_INTERFACE, &ret, NULL);
	if (ret == META_IFACE_FAILED)
	{
		g_SMAPI->Format(error, sizeof(error), "Missing Utils system plugin");
		ConColorMsg(Color(255, 0, 0, 255), "[%s] %s\n", GetLogTag(), error);
		std::string sBuffer = "meta unload "+std::to_string(g_PLID);
		engine->ServerCommand(sBuffer.c_str());
		return;
	}
	g_pMenus = (IMenusApi*)g_SMAPI->MetaFactory(Menus_INTERFACE, &ret, NULL);
	if (ret == META_IFACE_FAILED)
	{
		char error[64];
		V_strncpy(error, "Failed to lookup menus api. Aborting", 64);
		ConColorMsg(Color(255, 0, 0, 255), "[%s] %s\n", GetLogTag(), error);
		std::string sBuffer = "meta unload "+std::to_string(g_PLID);
		engine->ServerCommand(sBuffer.c_str());
		return;
	}
	g_pCP = (IChatProcessorApi *)g_SMAPI->MetaFactory(CP_INTERFACE, &ret, NULL);
	if (ret == META_IFACE_FAILED)
	{
		g_SMAPI->Format(error, sizeof(error), "Missing Chat Processor plugin");
		ConColorMsg(Color(255, 0, 0, 255), "[%s] %s\n", GetLogTag(), error);
		std::string sBuffer = "meta unload "+std::to_string(g_PLID);
		engine->ServerCommand(sBuffer.c_str());
		return;
	}
	g_pVIPCore = (IVIPApi *)g_SMAPI->MetaFactory(VIP_INTERFACE, &ret, NULL);
	if (ret == META_IFACE_FAILED)
	{
		g_SMAPI->Format(error, sizeof(error), "Missing VIP plugin");
		ConColorMsg(Color(255, 0, 0, 255), "[%s] %s\n", GetLogTag(), error);
		std::string sBuffer = "meta unload "+std::to_string(g_PLID);
		engine->ServerCommand(sBuffer.c_str());
		return;
	}
	LoadConfig();
	g_pUtils->AddChatListenerPre(g_PLID, OnChatPre);
	g_pVIPCore->VIP_OnClientLoaded(VIP_OnClientLoaded);
	g_pVIPCore->VIP_RegisterFeature("Chat", VIP_BOOL, SELECTABLE, OnSelect);
	g_pCP->HookOnChatMessage(g_PLID, OnChatMesssage);
	g_pUtils->StartupServer(g_PLID, StartupServer);
}

///////////////////////////////////////
const char* CP_VIP::GetLicense()
{
	return "GPL";
}

const char* CP_VIP::GetVersion()
{
	return "1.0";
}

const char* CP_VIP::GetDate()
{
	return __DATE__;
}

const char *CP_VIP::GetLogTag()
{
	return "CP_VIP";
}

const char* CP_VIP::GetAuthor()
{
	return "Pisex";
}

const char* CP_VIP::GetDescription()
{
	return "CP_VIP";
}

const char* CP_VIP::GetName()
{
	return "[Chat Processor] VIP";
}

const char* CP_VIP::GetURL()
{
	return "https://discord.gg/g798xERK5Y";
}
