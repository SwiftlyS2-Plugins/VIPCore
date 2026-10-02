#define CP_INTERFACE "IChatProcessorApi"

//return false to block sending message
typedef std::function<bool(int iSlot, std::string &szName, std::string &szMessage)> OnChatMessageCallback;

class IChatProcessorApi
{
public:
    virtual void HookOnChatMessage(SourceMM::PluginId id, OnChatMessageCallback callback) = 0;
};