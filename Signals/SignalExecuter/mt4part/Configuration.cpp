//+------------------------------------------------------------------+
//|                                       MetaTrader WebRegistration |
//|                 Copyright © 2001-2006, MetaQuotes Software Corp. |
//|                                        http://www.metaquotes.net |
//+------------------------------------------------------------------+
#include "stdafx.h"
#include "Configuration.h"
#include "common/stringfile.h"

CConfiguration ExtConfig;
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
CConfiguration::CConfiguration() : m_cfgs(NULL),m_cfgs_total(0),m_cfgs_max(0),m_cfgs_index(NULL)
  {
   m_filename[0]=0;
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
CConfiguration::~CConfiguration()
  {
//---- lock
   m_sync.Lock();
//---- delete all
   if(m_cfgs      !=NULL) { delete[] m_cfgs;       m_cfgs      =NULL; }
   if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
//---- set all to zero
   m_cfgs_total=m_cfgs_max=0;
//---- unlock
   m_sync.Unlock();
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Load(const char *filename)
  {
   CStringFile file;
   char        buffer[256],*start,*cp;
   PluginCfg   cfg={0},*temp;
//---- checks
   if(filename==NULL) return(FALSE);
//---- lock
   m_sync.Lock();
//---- set all to zero
   m_filename[0]=0; m_cfgs_total=0;
//---- delete index
   if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
//---- store filename
   COPY_STR(m_filename,filename);
//---- open file
   if(!file.Open(m_filename,GENERIC_READ,OPEN_ALWAYS))
     {
      m_sync.Unlock();
      return(FALSE);
     }
//---- reading configuration
   while(file.GetNextLine(buffer,sizeof(buffer)-1)>0)
     {
      //---- skip empty lines
      if(buffer[0]==';' || buffer[0]==0) continue;
      //---- terminate string
      TERMINATE_STR(buffer);
      //---- skip whitespaces
      start=buffer; while(*start==' ') start++;
      //---- find = and terminate by it
      if((cp=strstr(start,"="))==NULL) continue;
      *cp=0;
      //---- get parameter name
      COPY_STR(cfg.name,start);
      //---- skip whitspaces
      start=cp+1; while(*start==' ') start++;
      //---- receive parameter value
      COPY_STR(cfg.value,start);
      //---- set order
      cfg.reserved[0]=m_cfgs_total+1;
      //---- check space
      if(m_cfgs==NULL || m_cfgs_total>=m_cfgs_max)
        {
         //---- allocate new buffer
         if((temp=new PluginCfg[m_cfgs_total+1024])==NULL) { m_sync.Unlock(); return(FALSE); }
         //---- copy all from old buffer to new buffer and delete old
         if(m_cfgs!=NULL)
           {
            memcpy(temp,m_cfgs,sizeof(PluginCfg)*m_cfgs_total);
            delete[] m_cfgs;
           }
         //---- set new buffer
         m_cfgs    =temp;
         m_cfgs_max=m_cfgs_total+1024;
        }
      //---- add parameter
      memcpy(&m_cfgs[m_cfgs_total++],&cfg,sizeof(PluginCfg));
     }
//---- sort parameters by name
   qsort(m_cfgs,m_cfgs_total,sizeof(PluginCfg),SortByName);
//---- close file
   file.Close();
//---- unlock and return ok
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Total(void)
  {
   int total;
//---- get count of parameters in the lock
   m_sync.Lock();
   total=m_cfgs_total;
   m_sync.Unlock();
//---- return count
   return(total);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Add(const int pos,const PluginCfg *cfg)
  {
   PluginCfg *temp;
   int        i;
//---- checks
   if(cfg==NULL) return(FALSE);
//---- lock
   m_sync.Lock();
//---- check space
   if(m_cfgs==NULL || m_cfgs_total>=m_cfgs_max)
     {
      //---- allocate new buffer
      if((temp=new PluginCfg[m_cfgs_total+1024])==NULL) { m_sync.Unlock(); return(FALSE); }
      //---- copy all from old to new buffer and delete old
      if(m_cfgs!=NULL)
        {
         memcpy(temp,m_cfgs,sizeof(PluginCfg)*m_cfgs_total);
         delete[] m_cfgs;
        }
      //---- set new buffer
      m_cfgs    =temp;
      m_cfgs_max=m_cfgs_total+1024;
     }
//---- add parameter
   memcpy(&m_cfgs[m_cfgs_total],cfg,sizeof(PluginCfg));
//---- delete index
   if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
//---- set position
   m_cfgs[m_cfgs_total].reserved[0]=(pos<1?m_cfgs_total+1:pos);
//---- down all who position >= pos
   for(i=0;i<m_cfgs_total;i++)
      if(m_cfgs[i].reserved[0]>=m_cfgs[m_cfgs_total].reserved[0]) 
         m_cfgs[i].reserved[0]++;
//---- increment count of parameters
   m_cfgs_total++;
//---- resort parameters
   qsort(m_cfgs,m_cfgs_total,sizeof(PluginCfg),SortByName);
//---- save
   Save();
//---- unlock and return ok
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Delete(const char *name)
  {
   PluginCfg *temp;
//---- checks
   if(name==NULL) return(FALSE);
//---- lock
   m_sync.Lock();
//---- delete index
   if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
//---- find parameter
   if((temp=(PluginCfg *)bsearch(name,m_cfgs,m_cfgs_total,sizeof(PluginCfg),SortByName))==NULL)
     {
      m_sync.Unlock();
      return(FALSE);
     }
//---- delete parameter
   if(temp-m_cfgs<m_cfgs_total)
      memmove(temp,temp+1,sizeof(PluginCfg)*(m_cfgs_total-(temp-m_cfgs)-1));
//---- decrement count of paramters
   m_cfgs_total--;
//---- save
   Save();
//---- unlock and return tip-top
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Next(const int index,PluginCfg *cfg)
  {
   int i;
//---- checks
   if(index<0 || cfg==NULL) return(FALSE);
//---- lock
   m_sync.Lock();
//---- checks
   if(index>=m_cfgs_total) { m_sync.Unlock(); return(FALSE); }
//---- index not exists?
   if(m_cfgs_index==NULL)
     {
      //---- build index: allocate buffer
      if((m_cfgs_index=new PluginCfg*[m_cfgs_total])==NULL) { m_sync.Unlock(); return(FALSE); }
      //---- building
      for(i=0;i<m_cfgs_total;i++) m_cfgs_index[i]=&m_cfgs[i];
      //---- sort index
      qsort(m_cfgs_index,m_cfgs_total,sizeof(PluginCfg *),SortIndex);
      //---- correct order in original parameters
      for(i=0;i<m_cfgs_total;i++) m_cfgs_index[i]->reserved[0]=i+1;
     }
//---- give parameter
   memcpy(cfg,m_cfgs_index[index],sizeof(PluginCfg));
//---- unlock
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Get(const char *name,PluginCfg *cfg,const int pos)
  {
   PluginCfg *temp;
   int        i;
//---- checks
   if(name==NULL || cfg==NULL) return(FALSE);
//---- lock
   m_sync.Lock();
//---- find parameter
   if((temp=(PluginCfg *)bsearch(name,m_cfgs,m_cfgs_total,sizeof(PluginCfg),SortByName))==NULL)
     {
      m_sync.Unlock();
      return(FALSE);
     }
//---- give parameter
   memcpy(cfg,temp,sizeof(PluginCfg));
//---- position changed?
   if(pos>0 && temp->reserved[0]!=pos)
     {
      //---- set new position
      temp->reserved[0]=pos;
      //---- delete index
      if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
      //---- down all who position >= pos
      for(i=0;i<m_cfgs_total;i++)
         if(m_cfgs[i].reserved[0]>=pos && temp!=&m_cfgs[i])
            m_cfgs[i].reserved[0]++;
      //---- save
      Save();
     }
//---- unlock and return ok
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Set(const PluginCfg *cfgs,const int cfgs_total)
  {
   PluginCfg *temp;
   int        i;
//---- checks
   if(cfgs==NULL || cfgs_total<0) return(FALSE);
//---- lock
   m_sync.Lock();
//---- delete index
   if(m_cfgs_index!=NULL) { delete[] m_cfgs_index; m_cfgs_index=NULL; }
//---- check space
   if(m_cfgs==NULL || m_cfgs_total>=m_cfgs_max)
     {
      //---- allocate new buffer
      if((temp=new PluginCfg[m_cfgs_total+1024])==NULL) { m_sync.Unlock(); return(FALSE); }
      //---- delete old buffer
      if(m_cfgs!=NULL) delete[] m_cfgs;
      //---- set new buffer
      m_cfgs    =temp;
      m_cfgs_max=m_cfgs_total+1024;
     }
//---- copy new paramaters
   memcpy(m_cfgs,cfgs,sizeof(PluginCfg)*cfgs_total);
//---- set count
   m_cfgs_total=cfgs_total;
//---- set order
   for(i=0;i<m_cfgs_total;i++) m_cfgs[i].reserved[0]=i+1;
//---- sort by name
   qsort(m_cfgs,m_cfgs_total,sizeof(PluginCfg),SortByName);
//---- save
   Save();
//---- unlock and return ok
   m_sync.Unlock();
   return(TRUE);
  }
//+------------------------------------------------------------------+
//| Get integer by name                                              |
//+------------------------------------------------------------------+
int CConfiguration::GetInteger(const int pos,const char *name,int *value,const char *defvalue)
  {
   PluginCfg cfg={0};
//---- check
   if(name==NULL || value==NULL) return(FALSE);
//---- try get parameter
   if(Get(name,&cfg,pos)==FALSE)
     {
      //---- prepare new parameter
      COPY_STR(cfg.name,name);
      if(defvalue!=NULL) COPY_STR(cfg.value,defvalue);
      //---- add
      if(Add(pos,&cfg)==FALSE) return(FALSE);
     }
//---- receive value
   *value=atoi(cfg.value);
//---- return ok
   return(TRUE);
  }
//+------------------------------------------------------------------+
//| Get string by name                                               |
//+------------------------------------------------------------------+
int CConfiguration::GetString(const int pos,const char *name,char *value,const int size,const char *defvalue)
  {
   PluginCfg cfg={0};
//---- checks
   if(name==NULL || value==NULL || size<0) return(FALSE);
//---- try get parameter
   if(Get(name,&cfg,pos)==FALSE && defvalue!=NULL) // Добавили проверку на пустое defVal для поиска 
     {
      //---- prepare new parameter
      COPY_STR(cfg.name,name);
      if(defvalue!=NULL) COPY_STR(cfg.value,defvalue);
      //---- add
      if(Add(pos,&cfg)==FALSE) return(FALSE);
     }
//---- receive string
   strncpy_s(value, size, cfg.value , _TRUNCATE);
//---- return ok
   return(TRUE);
  }
//+------------------------------------------------------------------+
//| Get float by name                                                |
//+------------------------------------------------------------------+
int CConfiguration::GetFloat(const int pos,const char *name,double *value,const char *defvalue)
  {
   PluginCfg cfg={0};
//---- checks
   if(name==NULL || value==NULL) return(FALSE);
//---- try get parameter
   if(Get(name,&cfg,pos)==FALSE)
     {
      //---- prepare new parameter
      COPY_STR(cfg.name,name);
      if(defvalue!=NULL) COPY_STR(cfg.value,defvalue);
      //---- add
      if(Add(pos,&cfg)==FALSE) return(FALSE);
     }
//---- receive value
   *value=atof(cfg.value);
//---- return ok
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::Save(void)
  {
   CStringFile file;
   int         i;
   char        buffer[256];
//---- checks
   if(m_filename[0]==0 || m_cfgs==NULL || m_cfgs_total<0) return(FALSE);
//---- if index not exists then build it
   if(m_cfgs_index==NULL)
     {
      //---- build index: allocate buffer
      if((m_cfgs_index=new PluginCfg*[m_cfgs_total])==NULL) return(FALSE);
      //---- building index
      for(i=0;i<m_cfgs_total;i++) m_cfgs_index[i]=&m_cfgs[i];
      //---- sort index
      qsort(m_cfgs_index,m_cfgs_total,sizeof(PluginCfg *),SortIndex);
      //---- correct order in original parameters
      for(i=0;i<m_cfgs_total;i++) m_cfgs_index[i]->reserved[0]=i+1;
     }
//---- opening file
   if(!file.Open(m_filename,GENERIC_WRITE,CREATE_ALWAYS)) return(FALSE);
//---- writing parameters
   for(i=0;i<m_cfgs_total;i++)
     {
      //---- prepare string 
      _snprintf_s(buffer,sizeof(buffer)-1,"%s=%s\n",m_cfgs_index[i]->name,m_cfgs_index[i]->value);
      //---- store to disk
      file.Write(buffer,strlen(buffer));
     }
//---- close file
   file.Close();
//---- return ok
   return(TRUE);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::SortByName(const void *param1,const void *param2)
  {
   return strcmp(((PluginCfg *)param1)->name,((PluginCfg *)param2)->name);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
int CConfiguration::SortIndex(const void *param1,const void *param2)
  {
   return((*((PluginCfg **)param1))->reserved[0]-(*((PluginCfg **)param2))->reserved[0]);
  }
//+------------------------------------------------------------------+
//|                                                                  |
//+------------------------------------------------------------------+
